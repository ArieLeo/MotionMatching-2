using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;

namespace MotionMatching
{
    // Adjustment between Character Controller and Motion Matching Character Entity
    /* https://theorangeduck.com/page/code-vs-data-driven-displacement */

    public class SpringCharacterController : MonoBehaviour
    {
        // Events -----------------------------------------------------------
        public event Action<float> OnUpdate;

        // General ----------------------------------------------------------
        public int NumberPrediction = 3;
        public int PredictionFrames = 20;
        public float MaxSpeed = 1.0f;
        [Range(0.0f, 1.0f)] public float Responsiveness = 0.75f;
        public float MinimumVelocityClamp = 0.01f;
        // Adjustment & Clamping --------------------------------------------
        [Header("Adjustment")] // Move Simulation Bone towards the Simulation Object (motion matching towards character controller)
        public bool DoAdjustment = true;
        public MotionMatchingController SimulationBone; // MotionMatchingController's transform is the SimulationBone of the character
        [Range(0.0f, 1.0f)] public float PositionAdjustmentHalflife = 0.1f; // Time needed to move half of the distance between SimulationBone and SimulationObject
        // [Range(0.0f, 1.0f)] public float RotationAdjustmentHalflife = 0.2f;
        public bool DoClamping = true;
        [Range(0.0f, 1.0f)] public float MaxDistanceSimulationBoneAndObject = 0.1f; // Max distance between SimulationBone and SimulationObject
        [Header("DEBUG")]
        public bool DebugCurrent = true;
        public bool DebugPrediction = true;
        public bool DebugClamping = true;
        // --------------------------------------------------------------------------

        // PRIVATE ------------------------------------------------------------------
        // Input --------------------------------------------------------------------
        private float2 InputMovement;
        // Rotation and Predicted Rotation ------------------------------------------
        private quaternion DesiredRotation; // Desired Rotation/Direction
        private quaternion[] PredictedRotations;
        private float3 AngularVelocity;
        private float3[] PredictedAngularVelocities;
        // Position and Predicted Position ------------------------------------------
        private float2[] PredictedPosition;
        private float2 Velocity;
        private float2[] PredictedVelocity;
        private float2 Acceleration;
        private float2[] PredictedAcceleration;
        // Accumulated Delta Time --------------------------------------------------
        private Queue<float> LastDeltaTime = new Queue<float>();
        private float SumDeltaTime;

        // FUNCTIONS ---------------------------------------------------------------
        private void Start()
        {
            PredictedPosition = new float2[NumberPrediction];
            PredictedVelocity = new float2[NumberPrediction];
            PredictedAcceleration = new float2[NumberPrediction];
            DesiredRotation = quaternion.LookRotation(transform.forward, transform.up);
            PredictedRotations = new quaternion[NumberPrediction];
            PredictedAngularVelocities = new float3[NumberPrediction];
        }

        // Input a change in the movement direction
        public void SetMovementDirection(float2 movementDirection)
        {
            InputMovement = movementDirection;
        }

        private void Update()
        {
            // Average DeltaTime (for prediction... it is better to have a stable frame rate)
            float averagedDeltaTime = GetAveragedDeltaTime();

            // Rotations
            quaternion currentRotation = transform.rotation;
            PredictRotations(currentRotation, averagedDeltaTime);
            // Update Current Rotation
            quaternion newRot = ComputeNewRot(currentRotation);

            // Positions
            float2 desiredSpeed = InputMovement * MaxSpeed;
            float2 currentPos = new float2(transform.position.x, transform.position.z);
            // Predict
            PredictPositions(currentPos, desiredSpeed, averagedDeltaTime);
            // Update Current Position
            float2 newPos = ComputeNewPos(currentPos, desiredSpeed);

            // Update Character Controller
            if (math.lengthsq(Velocity) > MinimumVelocityClamp * MinimumVelocityClamp)
            {
                // Update Transform
                transform.position = new float3(newPos.x, transform.position.y, newPos.y);
                transform.rotation = newRot;
                // Desired Rotation
                float2 desiredDirection = math.normalize(Velocity);
                DesiredRotation = quaternion.LookRotation(new float3(desiredDirection.x, 0.0f, desiredDirection.y), transform.up);
            }

            // Adjust SimulationBone to pull the character (moving SimulationBone) towards the Simulation Object (character controller)
            if (DoAdjustment) AdjustSimulationBone();
            if (DoClamping) ClampSimulationBone();

            // Update other components depending on the character controller
            if (OnUpdate != null) OnUpdate(Time.deltaTime);
        }

        private void PredictRotations(quaternion currentRotation, float averagedDeltaTime)
        {
            for (int i = 0; i < NumberPrediction; i++)
            {
                // Init Predicted values
                PredictedRotations[i] = currentRotation;
                PredictedAngularVelocities[i] = AngularVelocity;
                // Predict
                Spring.SimpleSpringDamperImplicit(ref PredictedRotations[i], ref PredictedAngularVelocities[i],
                                                  DesiredRotation, 1.0f - Responsiveness, (i + 1) * NumberPrediction * averagedDeltaTime);
            }
        }

        /* https://theorangeduck.com/page/spring-roll-call#controllers */
        private void PredictPositions(float2 currentPos, float2 desiredSpeed, float averagedDeltaTime)
        {
            for (int i = 0; i < NumberPrediction; ++i)
            {
                if (i == 0)
                {
                    PredictedPosition[i] = currentPos;
                    PredictedVelocity[i] = Velocity;
                    PredictedAcceleration[i] = Acceleration;
                }
                else
                {
                    PredictedPosition[i] = PredictedPosition[i - 1];
                    PredictedVelocity[i] = PredictedVelocity[i - 1];
                    PredictedAcceleration[i] = PredictedAcceleration[i - 1];
                }
                Spring.CharacterPositionUpdate(ref PredictedPosition[i], ref PredictedVelocity[i], ref PredictedAcceleration[i],
                                               desiredSpeed, 1.0f - Responsiveness, PredictionFrames * averagedDeltaTime);
            }
        }

        private quaternion ComputeNewRot(quaternion currentRotation)
        {
            quaternion newRotation = currentRotation;
            Spring.SimpleSpringDamperImplicit(ref newRotation, ref AngularVelocity, DesiredRotation, 1.0f - Responsiveness, Time.deltaTime);
            return newRotation;
        }

        private float2 ComputeNewPos(float2 currentPos, float2 desiredSpeed)
        {
            float2 newPos = currentPos;
            Spring.CharacterPositionUpdate(ref newPos, ref Velocity, ref Acceleration, desiredSpeed, 1.0f - Responsiveness, Time.deltaTime);
            return newPos;
        }

        private void AdjustSimulationBone()
        {
            AdjustCharacterPosition();
            //AdjustCharacterRotation();
        }

        private void ClampSimulationBone()
        {
            // Clamp Position
            float3 simulationObject = transform.position;
            float3 simulationBone = SimulationBone.transform.position;
            if (math.distance(simulationObject, simulationBone) > MaxDistanceSimulationBoneAndObject)
            {
                SimulationBone.transform.position = MaxDistanceSimulationBoneAndObject * math.normalize(simulationBone - simulationObject) + simulationObject;
            }
        }

        private void AdjustCharacterPosition()
        {
            float3 simulationObject = transform.position;
            float3 simulationBone = SimulationBone.transform.position;
            float3 differencePosition = simulationObject - simulationBone;
            // Damp the difference using the adjustment halflife and dt
            float3 adjustmentPosition = Spring.DampAdjustmentImplicit(differencePosition, PositionAdjustmentHalflife, Time.deltaTime);
            // Move the simulation bone towards the simulation object
            SimulationBone.transform.position = simulationBone + adjustmentPosition;
        }

        // private void AdjustCharacterRotation()
        // {
        //     quaternion simulationObject = transform.rotation;
        //     quaternion simulationBone = SimulationBone.transform.rotation;
        //     // Find the difference in rotation (from character to simulation object)
        //     // Note: if numerically unstable, try quaternion.Normalize(quaternion.Inverse(simulationObject) * simulationBone)
        //     quaternion differenceRotation = quaternion.Inverse(simulationObject) * simulationBone;
        //     // Damp the difference using the adjustment halflife and dt
        //     quaternion adjustmentRotation = Spring.DampAdjustmentImplicit(differenceRotation, RotationAdjustmentHalflife, Time.deltaTime);
        //     // Rotate the simulation bone towards the simulation object
        //     SimulationBone.transform.rotation = simulationBone * adjustmentRotation;
        // }

        private float GetAveragedDeltaTime()
        {
            const int nAverageDeltaTime = 20;
            SumDeltaTime += Time.deltaTime;
            LastDeltaTime.Enqueue(Time.deltaTime);
            if (LastDeltaTime.Count == nAverageDeltaTime + 1) SumDeltaTime -= LastDeltaTime.Dequeue();
            return SumDeltaTime / nAverageDeltaTime;
        }

        public float2 GetWorldPredictedPosition(int index)
        {
            return PredictedPosition[index];
        }

        public float2 GetWorldPredictedDirection(int index)
        {
            float3 dir = math.mul(PredictedRotations[index], new float3(0, 0, 1));
            return math.normalize(new float2(dir.x, dir.z));
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            const float radius = 0.05f;
            const float vectorReduction = 0.5f;
            const float verticalOffset = 0.05f;
            Vector3 transformPos = transform.position + Vector3.up * verticalOffset;
            if (DebugCurrent)
            {
                // Draw Current Position & Velocity
                Gizmos.color = new Color(1.0f, 0.3f, 0.1f, 1.0f);
                Gizmos.DrawSphere(transformPos, radius);
                Gizmos.DrawLine(transformPos, transformPos + transform.forward * vectorReduction);
            }

            if (PredictedPosition == null || PredictedRotations == null) return;

            if (DebugPrediction)
            {
                // Draw Predicted Position & Velocity
                Gizmos.color = new Color(0.6f, 0.3f, 0.8f, 1.0f);
                for (int i = 0; i < PredictedPosition.Length; ++i)
                {
                    float3 predictedPos = new float3(PredictedPosition[i].x, verticalOffset, PredictedPosition[i].y);
                    float2 predictedDir = GetWorldPredictedDirection(i);
                    float3 predictedDir3D = new float3(predictedDir.x, 0.0f, predictedDir.y);
                    Gizmos.DrawSphere(predictedPos, radius);
                    Gizmos.DrawLine(predictedPos, predictedPos + predictedDir3D * vectorReduction);
                }
            }

            if (DebugClamping)
            {
                // Draw Clamp Circle
                if (DoClamping)
                {
                    Gizmos.color = new Color(0.1f, 1.0f, 0.1f, 1.0f);
                    GizmosExtensions.DrawWireCircle(transformPos, MaxDistanceSimulationBoneAndObject, quaternion.identity);
                }
            }
        }
#endif
    }
}
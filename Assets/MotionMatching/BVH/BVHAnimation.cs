using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace MotionMatching
{
    using Joint = Skeleton.Joint;

    /// <summary>
    /// Stores the BVH animation data in Unity format.
    /// </summary>
    public class BVHAnimation
    {
        public float FrameTime { get; private set; }
        public Skeleton Skeleton { get; private set; }
        public List<EndSite> EndSites { get; private set; }
        public Frame[] Frames { get; private set; }

        public BVHAnimation()
        {
            Skeleton = new Skeleton();
            EndSites = new List<EndSite>();
        }

        public void SetFrameTime(float frameTime)
        {
            FrameTime = frameTime;
        }

        public void InitFrames(int numberFrames)
        {
            Frames = new Frame[numberFrames];
        }

        public void AddFrame(int index, Frame frame)
        {
            Frames[index] = frame;
        }

        public void AddJoint(Joint joint)
        {
            Skeleton.AddJoint(joint);
        }

        public void AddEndSite(EndSite endSite)
        {
            EndSites.Add(endSite);
        }

        public void UpdateMecanimInformation(MotionMatchingData motionMatchingData)
        {
            for (int i = 0; i < Skeleton.Joints.Count; i++)
            {
                Joint joint = Skeleton.Joints[i];
                if (motionMatchingData.GetMecanimBone(joint.Name, out HumanBodyBones bone))
                {
                    joint.Type = bone;
                    Skeleton.Joints[i] = joint;
                }
            }
        }

        /// <summary>
        /// Apply forward kinematics to obtain the quaternion rotating from the local
        /// coordinate system of the joint to the world coordinate system.
        /// </summary>
        public quaternion GetWorldRotation(Joint joint, int frameIndex)
        {
            Frame frame = Frames[frameIndex];
            quaternion worldRot = quaternion.identity;

            while (joint.Index != 0) // while not root
            {
                worldRot = frame.LocalRotations[joint.Index] * worldRot;
                joint = Skeleton.GetParent(joint);
            }
            worldRot = frame.LocalRotations[0] * worldRot; // root

            return worldRot;
        }

        /// <summary>
        /// Apply forward kinematics to obtain the position of the joint in the world coordinate system.
        /// </summary>
        public float3 GetWorldPosition(Joint joint, int frameIndex)
        {
            Frame frame = Frames[frameIndex];
            float4x4 localToWorld = float4x4.identity;
            while (joint.ParentIndex != 0) // while not root
            {
                Joint parent = Skeleton.GetParent(joint);
                localToWorld = float4x4.TRS(parent.LocalOffset, frame.LocalRotations[joint.ParentIndex], new float3(1, 1, 1)) * localToWorld;
                joint = parent;
            }
            localToWorld = float4x4.TRS(frame.RootMotion, frame.LocalRotations[0], new float3(1, 1, 1)) * localToWorld;
            return math.mul(localToWorld, new float4(joint.LocalOffset, 1)).xyz;
        }

        public struct EndSite
        {
            public int ParentIndex;
            public Vector3 Offset;

            public EndSite(int parentIndex, Vector3 offset)
            {
                ParentIndex = parentIndex;
                Offset = offset;
            }
        }

        public struct Frame
        {
            public Vector3 RootMotion;
            public Quaternion[] LocalRotations;

            public Frame(Vector3 rootMotion, Quaternion[] localRotations)
            {
                RootMotion = rootMotion;
                LocalRotations = localRotations;
            }
        }
    }
}
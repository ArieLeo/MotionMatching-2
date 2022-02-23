using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
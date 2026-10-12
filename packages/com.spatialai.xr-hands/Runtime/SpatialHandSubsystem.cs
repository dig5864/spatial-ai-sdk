using System;
using System.Collections.Generic;
using SpatialAI.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.ProviderImplementation;

namespace SpatialAI.XRHands
{
    [Preserve]
    public sealed class SpatialHandSubsystem : XRHandSubsystem
    {
        public const string DescriptorId = "SpatialAI-Simulated-Hands";
        public const string UvcDescriptorId = "SpatialAI-UVC-Hands";
        public ISpatialHandsFrameSource FrameSource
        {
            get { return ((SpatialHandProvider)provider).Source; }
            set
            {
                if (running) throw new InvalidOperationException("Stop the subsystem before changing its source.");
                ((SpatialHandProvider)provider).Source = value;
            }
        }
        public SpatialHandsFrame LastFrame { get { return ((SpatialHandProvider)provider).LastFrame; } }

        protected override void OnStop()
        {
            var implementation = (SpatialHandProvider)provider;
            implementation.SuppressData = true;
            try { TryUpdateHands(UpdateType.Dynamic); }
            finally { base.OnStop(); implementation.SuppressData = false; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void RegisterDescriptor()
        {
            var descriptors = new List<XRHandSubsystemDescriptor>();
            SubsystemManager.GetSubsystemDescriptors(descriptors);
            foreach(var id in new[]{DescriptorId,UvcDescriptorId})
            {
                if(descriptors.Exists(d=>d.id==id))continue;
                XRHandSubsystemDescriptor.Register(new XRHandSubsystemDescriptor.Cinfo
                { id=id,providerType=typeof(SpatialHandProvider),subsystemTypeOverride=typeof(SpatialHandSubsystem) });
            }
        }
        public static SpatialHandSubsystem Create(ISpatialHandsFrameSource source)
        {
            if (source == null) throw new ArgumentNullException("source");
            RegisterDescriptor();
            var descriptors = new List<XRHandSubsystemDescriptor>();
            SubsystemManager.GetSubsystemDescriptors(descriptors);
            var id=source is UvcHandsFrameSource?UvcDescriptorId:DescriptorId;
            foreach (var descriptor in descriptors)
                if (descriptor.id == id)
                {
                    var subsystem = (SpatialHandSubsystem)descriptor.Create();
                    subsystem.FrameSource = source;
                    return subsystem;
                }
            throw new InvalidOperationException("Spatial hand descriptor was not registered.");
        }
    }

    [Preserve]
    public sealed class SpatialHandProvider : XRHandSubsystemProvider
    {
        internal ISpatialHandsFrameSource Source;
        internal SpatialHandsFrame LastFrame;
        internal bool SuppressData;
        public override void Start() { LastFrame = null; }
        public override void Stop() { LastFrame = null; }
        public override void Destroy() { Source = null; LastFrame = null; }
        public override void GetHandLayout(NativeArray<bool> jointsInLayout)
        {
            // Stage 1's synthetic source attempts all 26 joints. Missing samples
            // are temporary invalidity, not WillNeverBeValid or a changed layout.
            for (int i = 0; i < jointsInLayout.Length; i++) jointsInLayout[i] = false;
            for (int i = 0; i < (int)HandJointId.Count; i++)
                jointsInLayout[SpatialXRHandsContract.ToXRJointId((HandJointId)i).ToIndex()] = true;
        }
        public override XRHandSubsystem.UpdateSuccessFlags TryUpdateHands(XRHandSubsystem.UpdateType updateType,
            ref Pose leftHandRootPose, NativeArray<XRHandJoint> leftHandJoints,
            ref Pose rightHandRootPose, NativeArray<XRHandJoint> rightHandJoints)
        {
            SpatialHandsFrame frame = null;
            bool available = !SuppressData && Source != null && Source.TryGetLatestFrame(out frame);
            if (!available) frame = null;
            else if (Source is UvcHandsFrameSource ? frame.Source != HandFrameSource.MonocularEstimate : frame.Source != HandFrameSource.Simulation) frame = null;
            LastFrame = frame;
            var flags = XRHandSubsystem.UpdateSuccessFlags.None;
            if (Fill(frame == null ? null : frame.Left, HandSide.Left, ref leftHandRootPose, leftHandJoints))
                flags |= XRHandSubsystem.UpdateSuccessFlags.LeftHandRootPose | XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints;
            if (Fill(frame == null ? null : frame.Right, HandSide.Right, ref rightHandRootPose, rightHandJoints))
                flags |= XRHandSubsystem.UpdateSuccessFlags.RightHandRootPose | XRHandSubsystem.UpdateSuccessFlags.RightHandJoints;
            return flags;
        }
        private bool Fill(SpatialHandSample hand, HandSide side, ref Pose root, NativeArray<XRHandJoint> joints)
        {
            const HandDataFields poseFields = HandDataFields.Position | HandDataFields.Rotation;
            bool tracked = hand != null && hand.IsTracked && (hand.Root.Valid & poseFields) == poseFields &&
                Source.Capabilities.Validate(hand).Success;
            root = tracked ? ToPose(hand.Root) : Pose.identity;
            for (int i = 0; i < (int)HandJointId.Count; i++)
            {
                var id = SpatialXRHandsContract.ToXRJointId((HandJointId)i);
                var joint = tracked ? hand.GetJoint((HandJointId)i) : default(HandJointSample);
                var state = XRHandJointTrackingState.None;
                // XR Hands 1.4 has one Pose bit: both position and rotation must
                // exist. Do not fabricate an identity orientation for image points.
                if ((joint.Valid & poseFields) == poseFields) state |= XRHandJointTrackingState.Pose;
                if ((joint.Valid & HandDataFields.Radius) != 0) state |= XRHandJointTrackingState.Radius;
                if ((joint.Valid & HandDataFields.LinearVelocity) != 0) state |= XRHandJointTrackingState.LinearVelocity;
                if ((joint.Valid & HandDataFields.AngularVelocity) != 0) state |= XRHandJointTrackingState.AngularVelocity;
                joints[id.ToIndex()] = XRHandProviderUtility.CreateJoint(
                    SpatialXRHandsContract.ToXRHandedness(side), state, id,
                    (state & XRHandJointTrackingState.Pose) != 0 ? ToPose(joint) : Pose.identity,
                    (state & XRHandJointTrackingState.Radius) != 0 ? joint.Radius : 0,
                    (state & XRHandJointTrackingState.LinearVelocity) != 0 ? ToVector(joint.LinearVelocity) : Vector3.zero,
                    (state & XRHandJointTrackingState.AngularVelocity) != 0 ? ToVector(joint.AngularVelocity) : Vector3.zero);
            }
            return tracked;
        }
        private static Vector3 ToVector(HandVector3 v) { return new Vector3(v.X, v.Y, v.Z); }
        private static Pose ToPose(HandJointSample joint)
        {
            var q = joint.Orientation;
            return new Pose(ToVector(joint.Position), new Quaternion(q.X, q.Y, q.Z, q.W));
        }
    }
}

using System;
using SpatialAI.Core;
using UnityEngine.XR.Hands;

namespace SpatialAI.XRHands
{
    // Named mapping avoids assuming SDK zero-based indices match Unity enum values.
    // Mapping only; registration and source ownership live in SpatialHandSubsystem.
    public static class SpatialXRHandsContract
    {
        private static readonly XRHandJointID[] jointIds =
        {
            XRHandJointID.Wrist, XRHandJointID.Palm,
            XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip,
            XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip,
            XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip,
            XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip,
            XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip
        };
        public static XRHandJointID ToXRJointId(HandJointId id)
        {
            if ((int)id < 0 || id >= HandJointId.Count) throw new ArgumentOutOfRangeException("id");
            return jointIds[(int)id];
        }
        public static Handedness ToXRHandedness(HandSide side)
        {
            if (side == HandSide.Left) return Handedness.Left;
            if (side == HandSide.Right) return Handedness.Right;
            throw new ArgumentOutOfRangeException("side");
        }
        // Landmark correspondence only, not a coordinate/depth conversion.
        // -1 denotes one of the five points missing from the MediaPipe 21-point layout.
        public static int GetMediaPipeLandmarkIndex(HandJointId id)
        {
            return MonocularHandConverter.LandmarkIndex(id);
        }
    }
}

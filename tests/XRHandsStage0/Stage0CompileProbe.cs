using System;
using System.Collections.Generic;
using SpatialAI.Core;
using UnityEditor;
using UnityEngine;
#if SPATIALAI_STAGE0_WITH_XR_HANDS
using SpatialAI.XRHands;
using UnityEngine.XR.Hands;
#endif

public static class Stage0CompileProbe
{
    public static void Run()
    {
        if (!Application.unityVersion.StartsWith("2022.3.")) throw new Exception("This baseline requires Unity 2022.3.");
        if ((int)HandJointId.Count != 26) throw new Exception("Unexpected spatial layout.");
        var left = SpatialHandSample.Untracked(HandSide.Left);
        if (!SpatialHandCapabilities.None.Validate(left).Success) throw new Exception("Invalid absent hand.");
#if SPATIALAI_STAGE0_WITH_XR_HANDS
        var ids = new HashSet<XRHandJointID>();
        var landmarks = new HashSet<int>();
        int[] expectedLandmarks = { 0, -1, 1, 2, 3, 4, -1, 5, 6, 7, 8,
            -1, 9, 10, 11, 12, -1, 13, 14, 15, 16, -1, 17, 18, 19, 20 };
        int missing = 0;
        for (int i = 0; i < (int)HandJointId.Count; i++)
        {
            var id = (HandJointId)i;
            if (!ids.Add(SpatialXRHandsContract.ToXRJointId(id))) throw new Exception("Duplicate XR joint mapping.");
            if (SpatialXRHandsContract.ToXRJointId(id).ToString() != id.ToString()) throw new Exception("Wrong XR joint name.");
            int index = SpatialXRHandsContract.GetMediaPipeLandmarkIndex(id);
            if (index != expectedLandmarks[i]) throw new Exception("Wrong landmark correspondence.");
            if (index < 0) missing++;
            else if (index > 20 || !landmarks.Add(index)) throw new Exception("Invalid landmark correspondence.");
        }
        if (missing != 5 || landmarks.Count != 21 ||
            SpatialXRHandsContract.ToXRJointId(HandJointId.Wrist) != XRHandJointID.Wrist ||
            SpatialXRHandsContract.ToXRJointId(HandJointId.ThumbTip) != XRHandJointID.ThumbTip ||
            SpatialXRHandsContract.GetMediaPipeLandmarkIndex(HandJointId.IndexProximal) != 5 ||
            SpatialXRHandsContract.GetMediaPipeLandmarkIndex(HandJointId.LittleTip) != 20 ||
            SpatialXRHandsContract.ToXRHandedness(HandSide.Left) != Handedness.Left ||
            SpatialXRHandsContract.ToXRHandedness(HandSide.Right) != Handedness.Right)
            throw new Exception("Wrong joint/side correspondence.");
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count != 0) throw new Exception("Stage 0 must not start a hand subsystem.");
#endif
        Debug.Log("SPATIALAI_STAGE0_PASS Unity=" + Application.unityVersion);
        EditorApplication.Exit(0);
    }
}

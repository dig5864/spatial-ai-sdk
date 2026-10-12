#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using SpatialAI.XRHands;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Hands;
public sealed class Stage1PlayProbe : MonoBehaviour
{
    private IEnumerator Start()
    {
        // Let Start/Update render consumers discover the registered subsystem.
        yield return null; yield return null;
        try
        {
            var host = GetComponent<SpatialHandsSimulator>();
            if (host.Subsystem == null || !host.Subsystem.running) throw new Exception("Simulator did not start in Play mode.");
            int dynamicUpdates = 0, renderUpdates = 0;
            host.Subsystem.updatedHands += (s, f, type) =>
            { if (type == XRHandSubsystem.UpdateType.Dynamic) dynamicUpdates++; else renderUpdates++; };
            var duplicateObject = new GameObject("Duplicate simulator");
            var duplicate = duplicateObject.AddComponent<SpatialHandsSimulator>();
            if (duplicate.Subsystem != null) throw new Exception("Duplicate simulator started a second subsystem.");
            Destroy(duplicateObject);
            host.left.tracked = false;
            host.PublishAndUpdate(XRHandSubsystem.UpdateType.Dynamic);
            if (host.Subsystem.leftHand.isTracked) throw new Exception("Play mode hand loss failed.");
            for (int cycle = 0; cycle < 20; cycle++)
            {
                host.enabled = false;
                var instances = new List<XRHandSubsystem>(); SubsystemManager.GetSubsystems(instances);
                if (instances.FindAll(s => s is SpatialHandSubsystem).Count != 0) throw new Exception("Disable left a subsystem.");
                host.enabled = true;
                instances.Clear(); SubsystemManager.GetSubsystems(instances);
                if (instances.FindAll(s => s is SpatialHandSubsystem).Count != 1) throw new Exception("Re-enable duplicated a subsystem.");
            }
            host.left.tracked = true;
            host.PublishAndUpdate(XRHandSubsystem.UpdateType.Dynamic);
            host.PublishAndUpdate(XRHandSubsystem.UpdateType.BeforeRender);
            if (!host.Subsystem.leftHand.isTracked) throw new Exception("Play mode tracking recovery failed.");
            // The actual player loop is checked by a second component over frames.
            gameObject.AddComponent<Stage1LoopProbe>();
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}

#endif

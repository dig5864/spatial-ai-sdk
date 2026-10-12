#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using SpatialAI.XRHands;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Hands;
public sealed class Stage1LoopProbe : MonoBehaviour
{
    private int dynamicUpdates, renderUpdates, startFrame;
    private SpatialHandsSimulator host;
    private void Start()
    {
        host = GetComponent<SpatialHandsSimulator>(); startFrame = Time.frameCount;
        host.Subsystem.updatedHands += OnUpdate;
    }
    private void OnUpdate(XRHandSubsystem sender, XRHandSubsystem.UpdateSuccessFlags flags, XRHandSubsystem.UpdateType type)
    { if (type == XRHandSubsystem.UpdateType.Dynamic) dynamicUpdates++; else renderUpdates++; }
    private void Update()
    {
        if (Time.frameCount - startFrame < 8) return;
        try
        {
            if (dynamicUpdates < 5 || dynamicUpdates > 8) throw new Exception("Automatic Dynamic callback count incorrect: " + dynamicUpdates);
            if (renderUpdates < 5 || renderUpdates > 8) throw new Exception("Render-phase callback count incorrect: " + renderUpdates);
            var visualizer = FindObjectOfType<SpatialXRHandsVisualizer>();
            if (visualizer == null || !GameObject.Find("Left IndexTip").activeInHierarchy) throw new Exception("Standard API visualizer did not display tracked joints.");
            Debug.Log("SPATIALAI_STAGE1_PLAY_PASS dynamic=" + dynamicUpdates + " beforeRender=" + renderUpdates + " lifecycleCycles=20");
            host.Subsystem.updatedHands -= OnUpdate;
            host.StopSimulation();
            new GameObject("Persistent Scene Validation").AddComponent<Stage1SceneProbe>();
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        enabled = false;
    }
}

#endif

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SpatialAI.XRHands;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Hands;

public sealed class Stage1SceneProbe : MonoBehaviour
{
    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        for (int cycle = 0; cycle < 10; cycle++)
        {
            yield return SceneManager.LoadSceneAsync("SpatialHandsSimulation", LoadSceneMode.Single);
            yield return null;
            try
            {
                var instances = new List<XRHandSubsystem>(); SubsystemManager.GetSubsystems(instances);
                if (instances.FindAll(s => s is SpatialHandSubsystem && s.running).Count != 1)
                    throw new Exception("Scene reload duplicated or lost the subsystem.");
                var host = FindObjectOfType<SpatialHandsSimulator>();
                if (!host.Subsystem.leftHand.isTracked || !host.Subsystem.rightHand.isTracked)
                    throw new Exception("Reloaded scene did not acquire hands.");
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        try
        {
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) CaptureScene();
            FindObjectOfType<SpatialHandsSimulator>().StopSimulation();
            Debug.Log("SPATIALAI_STAGE1_SCENES_PASS reloads=10 graphics=" + SystemInfo.graphicsDeviceType);
            UnityEditor.SessionState.SetBool("SpatialStage1.PlayPassed", true);
            EditorApplication.ExitPlaymode();
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static void CaptureScene()
    {
        var camera = Camera.main;
        var target = new RenderTexture(800, 600, 24);
        var image = new Texture2D(800, 600, TextureFormat.RGB24, false);
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 800, 600), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "../simulation-preview.png"), image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            target.Release(); Destroy(target); Destroy(image);
        }
    }
}
#endif

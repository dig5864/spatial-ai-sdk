#if SPATIAL_VUFORIA_DEMO
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SpatialAI.Unity;
using UnityEngine;

// A separate build-only scene bootstrap. Configuration must be supplied on the device.
public sealed class SpatialVuforiaDemo : MonoBehaviour
{
    [Serializable] private sealed class Config
    {
        public string license;
        public float targetWidthMeters;
        public float detailTargetWidthMeters;
    }
    private string message = "Waiting for configuration";
    private SpatialImageTracking tracker;
    private bool attempting;
    private float nextPoseLog;
    private IReadOnlyList<SpatialVuforiaTargetCatalog.Target> targets;
    private string ConfigPath => Path.Combine(Application.persistentDataPath, "vuforia-config.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() { new GameObject("Vuforia demo").AddComponent<SpatialVuforiaDemo>(); }
    private IEnumerator Start()
    {
        while (Camera.main == null) yield return null;
        var mount = new GameObject("UVC Camera Mount").transform;
        mount.SetParent(Camera.main.transform, false);
        var anchor = new GameObject("Image Target Anchor").transform;
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var shader = Resources.Load<Shader>("SpatialVuforiaSolid");
        if (shader != null)
        {
            var material = new Material(shader);
            material.color = new Color(0.2f, 0.9f, 0.35f, 1f);
            cube.GetComponent<Renderer>().sharedMaterial = material;
        }
        else Debug.LogError("SpatialVuforia: test cube shader is missing");
        cube.transform.SetParent(anchor, false);
        cube.transform.localScale = Vector3.one * 0.025f;
        cube.transform.localPosition = Vector3.zero;
        tracker = gameObject.AddComponent<SpatialImageTracking>();
        tracker.CameraMount = mount; tracker.TargetAnchor = anchor;
        anchor.gameObject.SetActive(false);
        TryStart();
    }
    private void TryStart()
    {
        if (attempting || tracker == null) return;
        if (!File.Exists(ConfigPath)) { message = "Missing vuforia-config.json: " + ConfigPath; return; }
        try
        {
            var config = JsonUtility.FromJson<Config>(File.ReadAllText(ConfigPath));
            if (config == null || string.IsNullOrEmpty(config.license)) { message = "Config needs a license"; return; }
            if (targets == null) targets = SpatialVuforiaTargetCatalog.LoadBuiltIn();
            if (targets.Count < 2) throw new InvalidDataException("The validation scene requires two SDK targets.");
            float firstWidth = config.targetWidthMeters > 0 ? config.targetWidthMeters : targets[0].DefaultWidthMm * .001f;
            float secondWidth = config.detailTargetWidthMeters > 0 ? config.detailTargetWidthMeters : targets[1].DefaultWidthMm * .001f;
            tracker.StartTracking(config.license, targets[0].MaterializeImage(), firstWidth,
                secondImagePath: targets[1].MaterializeImage(), secondWidthM: secondWidth);
            attempting = true; message = "Starting image tracking";
        }
        catch (Exception e) { message = "Cannot start: " + e.Message; }
    }
    private void OnGUI()
    {
        GUI.skin.label.fontSize = 22; GUI.skin.button.fontSize = 22;
        GUI.Box(new Rect(10,10,Math.Min(Screen.width-20,900),140), "Vuforia UVC test (experimental)");
        GUI.Label(new Rect(25,40,Screen.width-50,60), attempting && tracker != null ? tracker.State : message);
        if (!attempting && GUI.Button(new Rect(25,100,180,40), "Retry")) TryStart();
    }
    private void Update()
    {
        if (tracker == null || tracker.NativeStatus != 3 || tracker.TargetAnchor == null ||
            !tracker.TargetAnchor.gameObject.activeSelf || Time.realtimeSinceStartup < nextPoseLog) return;
        nextPoseLog = Time.realtimeSinceStartup + 2f;
        Vector3 cameraTarget = tracker.TargetAnchor.localPosition;
        Vector3 headTarget = tracker.CameraMount.localPosition + tracker.CameraMount.localRotation * cameraTarget;
        Debug.Log("SpatialVuforia pose target="+tracker.TargetId+" cameraFromTarget=" + cameraTarget.ToString("F4") +
            " headFromTarget=" + headTarget.ToString("F4") + " cameraMount=" + tracker.CameraMount.localPosition.ToString("F4"));
    }
    private void OnApplicationPause(bool paused)
    {
        if (!paused && tracker != null) StartCoroutine(RetryAfterResume());
    }
    private IEnumerator RetryAfterResume()
    {
        yield return new WaitForSecondsRealtime(1f);
        if (tracker != null && tracker.State == "stopped") { attempting = false; TryStart(); }
    }
}
#endif

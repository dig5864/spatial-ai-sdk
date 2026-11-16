#if SPATIAL_VUFORIA_DEMO
using System;
using System.Collections;
using System.IO;
using SpatialAI.Core;
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
    [Serializable] private sealed class Bundle { public Intrinsics intrinsics; public Setup setup; public HandTracking handTracking; }
    [Serializable] private sealed class Intrinsics { public int cameraMountRotationDeg = -1; }
    [Serializable] private sealed class Setup { public int cameraMountRotationDeg = -1; }
    [Serializable] private sealed class HandTracking { public string displayReflection, handednessMapping; }
    public static UvcHandCalibration Calibration { get; private set; }
    public static bool CalibrationReady { get; private set; }
    public static bool HandDisplayReflectX { get; private set; }
    public static string CalibrationError { get; private set; }
    private string message = "Waiting for configuration";
    private SpatialImageTracking tracker;
    private bool attempting;
    private float nextPoseLog;
    private string ConfigPath => Path.Combine(Application.persistentDataPath, "vuforia-config.json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureHandMount()
    {
        try
        {
            string path = SpatialUvcCalibrationStore.DefaultPath;
            if (!File.Exists(path)) throw new InvalidOperationException("Calibration file is missing: " + path);
            string json = File.ReadAllText(path);
            var bundle = JsonUtility.FromJson<Bundle>(json);
            var calibration = SpatialUvcCalibrationLoader.Load(json);
            if (bundle == null || bundle.intrinsics == null || bundle.setup == null || bundle.handTracking == null ||
                bundle.intrinsics.cameraMountRotationDeg != bundle.setup.cameraMountRotationDeg ||
                (bundle.intrinsics.cameraMountRotationDeg != 0 && bundle.intrinsics.cameraMountRotationDeg != 90 &&
                 bundle.intrinsics.cameraMountRotationDeg != 180 && bundle.intrinsics.cameraMountRotationDeg != 270) ||
                (bundle.handTracking.displayReflection != "none" && bundle.handTracking.displayReflection != "horizontal") ||
                (bundle.handTracking.handednessMapping != "direct" && bundle.handTracking.handednessMapping != "swap") ||
                calibration == null || calibration.imageWidth != 640 || calibration.imageHeight != 480 ||
                calibration.headFromCameraTranslationMetres == null || calibration.headFromCameraTranslationMetres.Length != 3 ||
                calibration.headFromCameraRotationXyzw == null || calibration.headFromCameraRotationXyzw.Length != 4)
                throw new InvalidOperationException("Calibration lacks 640x480 camera, mount rotation or explicit hand mapping");
            SpatialHandTracking.UvcMountRotationDegrees = bundle.intrinsics.cameraMountRotationDeg;
            SpatialHandTracking.UvcMirrorOutputX = calibration.inputMirrored;
            calibration.imageRotationClockwise = bundle.intrinsics.cameraMountRotationDeg;
            calibration.swapHandedness = bundle.handTracking.handednessMapping == "swap";
            HandDisplayReflectX = bundle.handTracking.displayReflection == "horizontal";
            Calibration = calibration; CalibrationReady = true;
            Debug.Log("SpatialVuforia: camera calibration and explicit hand mapping loaded");
        }
        catch (Exception error)
        {
            CalibrationReady = false; Calibration = null; CalibrationError = error.Message;
            Debug.LogError("SpatialVuforia calibration: " + error.Message);
        }
    }
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
        LoadTarget();
        TryStart();
    }
    private void LoadTarget()
    {
        foreach (string name in new[] { "blue-card-original", "detail-85mm-control" })
        {
            var target = Resources.Load<TextAsset>(name);
            if (target == null) { message = "Target image missing: " + name; return; }
            File.WriteAllBytes(Path.Combine(Application.persistentDataPath, name + ".png"), target.bytes);
        }
    }
    private void TryStart()
    {
        if (attempting || tracker == null) return;
        if (!CalibrationReady) { message = "Calibration error: " + CalibrationError; return; }
        if (!File.Exists(ConfigPath)) { message = "Missing vuforia-config.json: " + ConfigPath; return; }
        try
        {
            var config = JsonUtility.FromJson<Config>(File.ReadAllText(ConfigPath));
            if (config == null || string.IsNullOrEmpty(config.license) || config.targetWidthMeters <= 0 || config.detailTargetWidthMeters <= 0)
            { message = "Config needs license and both target widths"; return; }
            var c = Calibration;
            var t = c.headFromCameraTranslationMetres; var q = c.headFromCameraRotationXyzw;
            tracker.CameraMount.localPosition = new Vector3(t[0],t[1],t[2]);
            tracker.CameraMount.localRotation = new Quaternion(q[0],q[1],q[2],q[3]);
            var intrinsics = new float[12];
            intrinsics[0] = c.fx; intrinsics[1] = c.fy; intrinsics[2] = c.cx; intrinsics[3] = c.cy;
            Array.Copy(c.distortion, 0, intrinsics, 4, c.distortion.Length);
            tracker.StartTracking(config.license, Path.Combine(Application.persistentDataPath,"blue-card-original.png"),
                config.targetWidthMeters, intrinsics, secondImagePath: Path.Combine(Application.persistentDataPath,"detail-85mm-control.png"),
                secondWidthM: config.detailTargetWidthMeters);
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

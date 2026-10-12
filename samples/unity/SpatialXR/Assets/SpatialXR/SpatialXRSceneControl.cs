using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using SpatialAI.Unity;

namespace SpatialAI.Samples
{
    [Preserve]
    public sealed class SpatialXRSceneControl : MonoBehaviour
    {
        private string requestedScene;
        private AsyncOperation loading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var control = new GameObject("SpatialXRSceneControl");
            DontDestroyOnLoad(control);
            control.AddComponent<SpatialXRSceneControl>();
        }

        private void Awake()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var receiver = new AndroidJavaClass("com.spatialai.samples.SpatialXRSceneControl"))
                receiver.CallStatic("register", activity);
#endif
            Debug.Log("Spatial XR scene control ready: imu / hands / xrhands / xri / optics / calibration / capture");
        }

        // Invoked by Android UnitySendMessage; keep this method in IL2CPP builds.
        [Preserve]
        public void SwitchScene(string scene)
        {
            var parts = (scene ?? "").Split('|');
            var gestures = parts.Length < 2 || parts[1] != "0";
            scene = parts[0];
            string target;
            switch ((scene ?? "").Trim().ToLowerInvariant())
            {
                case "imu": case "imudiagnostics": target = "ImuDiagnostics"; break;
                case "hands": case "hellospatialxr": target = "HelloSpatialXR"; break;
                case "xrhands": case "uvcxrhands": target = "SpatialUvcXRHands"; break;
                case "xri": case "interaction": target = "SpatialHandInteraction"; break;
                case "optics": case "opticscomparison": target = "OpticsComparison"; break;
                case "calibration": target = "SpatialUvcCalibration"; break;
                case "capture":
                    var recorder = FindObjectOfType<SpatialUvcCalibrationRecorder>();
                    if (recorder != null) recorder.Capture();
                    else Debug.LogWarning("SpatialCalibration: switch to calibration before capture.");
                    return;
                default:
                    Debug.LogWarning("Spatial XR scene switch rejected: " + scene);
                    return;
            }
            if (!Application.CanStreamedLevelBeLoaded(target))
            {
                Debug.LogWarning("Spatial XR scene switch rejected: scene not included in APK: " + target);
                return;
            }
            var backend=SpatialHandTracking.InferenceBackend;
            if(parts.Length>=5&&!string.IsNullOrWhiteSpace(parts[4])&&
                (!System.Enum.TryParse(parts[4],true,out backend)||!System.Enum.IsDefined(typeof(SpatialHandInferenceBackend),backend)))
            {Debug.LogWarning("Spatial XR inference request rejected: use CPU or GPU.");return;}
            if (parts.Length >= 4)
            {
                int mode, timeUs;
                if (!int.TryParse(parts[2], out mode) || !int.TryParse(parts[3], out timeUs) ||
                    (mode != -1 && mode != 1 && mode != 2 && mode != 4 && mode != 8) ||
                    (timeUs != -1 && (timeUs <= 0 || timeUs % 100 != 0)) ||
                    (timeUs != -1 && (mode == 2 || mode == 8)))
                {
                    Debug.LogWarning("Spatial XR exposure request rejected: use mode 1/2/4/8 and positive exposureTimeUs in multiples of 100; time requires manual exposure.");
                    return;
                }
                if (mode != -1 || timeUs != -1)
                    SpatialHandTracking.ConfigureUvcExposure(mode, timeUs);
            }
            requestedScene = target;
            SpatialHandTracking.GestureRecognitionEnabled = gestures;
            SpatialHandTracking.InferenceBackend=backend;
            Debug.Log("Spatial XR inference backend requested: "+backend);
            Debug.Log("Spatial XR gestures enabled: " + gestures);
            Debug.Log("Spatial XR scene switch requested: " + target);
        }

        private void Update()
        {
            if (loading != null)
            {
                if (!loading.isDone) return;
                loading = null;
                Debug.Log("Spatial XR scene active: " + SceneManager.GetActiveScene().name);
            }
            if (requestedScene == null) return;
            var target = requestedScene;
            requestedScene = null;
            if (SceneManager.GetActiveScene().name == target)
            {
                Debug.Log("Spatial XR scene already active: " + target);
                return;
            }
            loading = SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var receiver = new AndroidJavaClass("com.spatialai.samples.SpatialXRSceneControl"))
                receiver.CallStatic("unregister");
#endif
        }
    }
}

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
        private bool forceSceneReload;
        private bool? smoothingOverride;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var identity=SpatialDeviceProfiles.ReadHostIdentity();
            bool rhinoX=string.Equals(identity.Model,"RhinoX",System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(identity.DeviceCode,"kona",System.StringComparison.OrdinalIgnoreCase);
            SpatialHandTracking.CameraSource = rhinoX ? SpatialHandCameraSource.HostProfile : SpatialHandCameraSource.Uvc;
            var control = new GameObject("SpatialXRSceneControl");
            DontDestroyOnLoad(control);
            control.AddComponent<SpatialXRSceneControl>();
        }

        private void Awake()
        {
            gameObject.AddComponent<SpatialFrameRateDebugControls>();
            SceneManager.sceneLoaded+=OnSceneLoaded;
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var receiver = new AndroidJavaClass("com.spatialai.samples.SpatialXRSceneControl"))
                receiver.CallStatic("register", activity);
#endif
            Debug.Log("Spatial XR scene control ready: imu / hands / xrhands / xri / optics / calibration / capture");
        }

        [Preserve]
        public void ConfigureFrameRate(string value)
        {
            GetComponent<SpatialFrameRateDebugControls>().Configure(value);
        }
        [Preserve]
        public void ShowFrameRateSettings(string value)
        {
            if (bool.TryParse(value, out var visible))
                GetComponent<SpatialFrameRateDebugControls>().ShowSettings(visible);
        }
        [Preserve]
        public void SetPhoneStereoMode(string mode)
        {
            if(System.Enum.TryParse(mode,true,out SpatialAI.Cardboard.PhoneStereoDiagnosticMode parsed) &&
                System.Enum.IsDefined(typeof(SpatialAI.Cardboard.PhoneStereoDiagnosticMode),parsed))
                SpatialAI.Cardboard.SpatialCardboardBridge.SetPhoneStereoDiagnosticMode(parsed);
            else Debug.LogWarning("Spatial phone stereo mode rejected: use Profile/Normal/Zero/Reverse");
        }
        [Preserve]
        public void SetHandSmoothing(string value)
        {
            if(!bool.TryParse(value,out var enabled))return;
            smoothingOverride=enabled;ApplySmoothing();
            Debug.Log("Spatial XR One Euro requested: "+enabled);
        }
        private void ApplySmoothing()
        {
            if(!smoothingOverride.HasValue)return;
            foreach(var driver in FindObjectsOfType<SpatialAI.XRHands.SpatialUvcHands>())
            {
                if(driver.smoothing==null)driver.smoothing=new SpatialHandSmoothing();
                driver.smoothing.enabled=smoothingOverride.Value;
                Debug.Log("Spatial XR One Euro applied: "+driver.smoothing.enabled+" minCutoffHz="+driver.smoothing.minCutoffHz+" beta="+driver.smoothing.beta);
            }
        }
        private void OnSceneLoaded(Scene scene,LoadSceneMode mode){StartCoroutine(ApplySmoothingAfterStart());}
        private System.Collections.IEnumerator ApplySmoothingAfterStart(){yield return null;ApplySmoothing();}
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
                case "imu": case "imudiagnostics": ImuDiagnostics.StereoProbeOnly=false; target = "ImuDiagnostics"; break;
                case "stereo": ImuDiagnostics.StereoProbeOnly=true; forceSceneReload=true; target="ImuDiagnostics"; break;
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
            var maxHands = SpatialHandTracking.MaxHands;
            var scheduling = SpatialHandTracking.SchedulingMode;
            // Existing explicit maxHands requests retain fixed-mode comparison behavior.
            if (parts.Length >= 6 && !string.IsNullOrWhiteSpace(parts[5])) scheduling = SpatialHandSchedulingMode.Fixed;
            if (parts.Length >= 9 && !string.IsNullOrWhiteSpace(parts[8]) &&
                (!System.Enum.TryParse(parts[8], true, out scheduling) || !System.Enum.IsDefined(typeof(SpatialHandSchedulingMode), scheduling)))
            { Debug.LogWarning("Spatial XR scheduling request rejected: use Auto or Fixed."); return; }
            var modelVariant = SpatialHandTracking.ModelVariant;
            if (parts.Length >= 10 && !string.IsNullOrWhiteSpace(parts[9]) &&
                (!System.Enum.TryParse(parts[9], true, out modelVariant) || !System.Enum.IsDefined(typeof(SpatialHandModelVariant), modelVariant)))
            { Debug.LogWarning("Spatial XR model request rejected: use FP16 or INT8."); return; }
            var cameraSizeTier = SpatialHandTracking.CameraSizeTier;
            var cameraSource = SpatialHandTracking.CameraSource;
            if (parts.Length >= 8 && !string.IsNullOrWhiteSpace(parts[7]))
            {
                switch (parts[7].Trim().ToLowerInvariant())
                {
                    case "uvc": cameraSource = SpatialHandCameraSource.Uvc; break;
                    case "rear": cameraSource = SpatialHandCameraSource.Rear; break;
                    case "front": cameraSource = SpatialHandCameraSource.Front; break;
                    default: Debug.LogWarning("Spatial XR camera source rejected: use uvc/rear/front."); return;
                }
            }
            if (parts.Length >= 7 && !string.IsNullOrWhiteSpace(parts[6]) &&
                (!int.TryParse(parts[6], out cameraSizeTier) || cameraSizeTier < 1 || cameraSizeTier > 3))
            { Debug.LogWarning("Spatial XR camera size request rejected: use cameraSizeTier 1/2/3."); return; }
            if (parts.Length >= 6 && !string.IsNullOrWhiteSpace(parts[5]) &&
                (!int.TryParse(parts[5], out maxHands) || (maxHands != 1 && maxHands != 2)))
            { Debug.LogWarning("Spatial XR hand count request rejected: use maxHands 1 or 2."); return; }
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
            SpatialHandTracking.ModelVariant = modelVariant;
            Debug.Log("Spatial XR model requested: " + modelVariant);
            SpatialHandTracking.MaxHands = maxHands;
            SpatialHandTracking.SchedulingMode = scheduling;
            SpatialHandTracking.CameraSizeTier = cameraSizeTier;
            SpatialHandTracking.CameraSource = cameraSource;
            Debug.Log("Spatial XR camera source requested: " + cameraSource);
            Debug.Log("Spatial XR camera size tier requested: " + cameraSizeTier);
            Debug.Log("Spatial XR maximum hands requested: " + maxHands);
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
            if (SceneManager.GetActiveScene().name == target && !forceSceneReload)
            {
                Debug.Log("Spatial XR scene already active: " + target);
                return;
            }
            forceSceneReload=false;
            loading = SceneManager.LoadSceneAsync(target, LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded-=OnSceneLoaded;
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var receiver = new AndroidJavaClass("com.spatialai.samples.SpatialXRSceneControl"))
                receiver.CallStatic("unregister");
#endif
        }
    }
}

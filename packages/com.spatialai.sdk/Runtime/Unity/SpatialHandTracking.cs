using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace SpatialAI.Unity
{
    public enum SpatialHandInferenceBackend { CPU, GPU }
    public enum SpatialGestureKind
    {
        None,
        ClosedFist,
        OpenPalm,
        PointingUp,
        ThumbDown,
        ThumbUp,
        Victory,
        ILoveYou,
        Unknown
    }

    [Serializable]
    public sealed class SpatialHandJoint
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public sealed class SpatialHand
    {
        public SpatialHandJoint[] joints;
        public string rawHandedness;
        public float handednessScore;
        public bool hasHandednessScore;
        // Main-thread association: preserve the model's original label for diagnostics.
        public string stabilizedHandedness;
        public int trackingId;
        public string EffectiveHandedness { get { return string.IsNullOrEmpty(stabilizedHandedness) ? rawHandedness : stabilizedHandedness; } }
        public string rawGesture;
        public float gestureScore;

        /// <summary>MediaPipe's per-frame category; Pinching is calculated separately from landmarks.</summary>
        public SpatialGestureKind Gesture
        {
            get
            {
                switch (rawGesture)
                {
                    case null:
                    case "":
                    case "None": return SpatialGestureKind.None;
                    case "Closed_Fist": return SpatialGestureKind.ClosedFist;
                    case "Open_Palm": return SpatialGestureKind.OpenPalm;
                    case "Pointing_Up": return SpatialGestureKind.PointingUp;
                    case "Thumb_Down": return SpatialGestureKind.ThumbDown;
                    case "Thumb_Up": return SpatialGestureKind.ThumbUp;
                    case "Victory": return SpatialGestureKind.Victory;
                    case "ILoveYou": return SpatialGestureKind.ILoveYou;
                    default: return SpatialGestureKind.Unknown;
                }
            }
        }

        public bool Pinching
        {
            get
            {
                if (joints == null || joints.Length < 18 || joints[4] == null ||
                    joints[5] == null || joints[8] == null || joints[17] == null) return false;
                var dx = joints[4].x - joints[8].x;
                var dy = joints[4].y - joints[8].y;
                var palmX = joints[5].x - joints[17].x;
                var palmY = joints[5].y - joints[17].y;
                var palmSquared = palmX * palmX + palmY * palmY;
                return palmSquared > 0.0001f &&
                    dx * dx + dy * dy < palmSquared * 0.4f * 0.4f;
            }
        }
    }

    [Serializable]
    public sealed class SpatialHandFrame
    {
        public string cameraId;
        public int imageWidth;
        public int imageHeight;
        public long sequence;
        public long timestampNs;
        public long receivedAtNs;
        public float resultFps;
        public float conversionMs;
        public float inferenceMs;
        public float captureToResultMs;
        public string gestureSource;
        public string runningMode;
        public string @delegate;
        public long receivedHostStopwatchNs;
        public float resultAgeAtReceiveMs=-1;
        public SpatialHand[] hands;
    }

    /// <summary>Image-space hands from the Android RGB camera. No metric head-space pose is claimed.</summary>
    [DisallowMultipleComponent]
    public sealed class SpatialHandTracking : MonoBehaviour
    {
        public static bool GestureRecognitionEnabled { get; set; } = true;
        private static SpatialHandInferenceBackend inferenceBackend=SpatialHandInferenceBackend.CPU;
        /// <summary>Provider configuration; application hand data still uses standard XR Hands APIs.</summary>
        public static SpatialHandInferenceBackend InferenceBackend
        {
            get { return inferenceBackend; }
            set
            {
                if(value!=SpatialHandInferenceBackend.CPU&&value!=SpatialHandInferenceBackend.GPU)
                    throw new ArgumentOutOfRangeException(nameof(value));
                inferenceBackend=value;
            }
        }
        private static int exposureMode = -1, exposureTimeUs = -1, exposureRevision;
        public static void ConfigureUvcExposure(int mode, int timeUs)
        {
            exposureMode = mode; exposureTimeUs = timeUs; exposureRevision++;
        }
        public SpatialHandFrame Latest { get; private set; }
        public string State { get; private set; } = "idle";
        public event Action<SpatialHandFrame> FrameUpdated;
        public event Action<string> Invalidated;
        private static SpatialHandTracking cameraOwner;
        public Texture2D PreviewTexture { get; private set; }
        public float CaptureFps { get; private set; }
        public float PreviewFps { get; private set; }
        public long PreviewSequence { get { return previewSequence; } }
        private long previewSequence = -1;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject bridge;
        private float nextPoll;
        private long lastSequence = -1;
        private bool paused;
        private bool externalCamera;
        private bool appliedGestureEnabled;
        private SpatialHandInferenceBackend appliedInferenceBackend;
        private int appliedExposureRevision;
        private float previewWindowStart;
        private int previewCount;

        private void Update()
        {
            if (paused) return;
            if(cameraOwner!=null && cameraOwner!=this)
            { State="camera owned by another SpatialHandTracking";return; }
            cameraOwner=this;
            if (bridge == null)
            {
                if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                {
                    State = "waiting for camera permission";
                    if (!permissionRequested)
                    {
                        permissionRequested = true;
                        Permission.RequestUserPermission(Permission.Camera);
                    }
                    return;
                }
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    bridge = new AndroidJavaObject("com.spatialai.hands.SpatialHandBridge",
                        player.GetStatic<AndroidJavaObject>("currentActivity"));
                var hostResult = SpatialDeviceProfiles.SelectHost(SpatialDeviceProfiles.ReadHostIdentity(), out var host);
                externalCamera = hostResult.Success && host.cameraSource == "external-uvc";
                if (hostResult.Success) bridge.Call("setCameraSource", host.cameraSource);
                appliedGestureEnabled = GestureRecognitionEnabled;
                bridge.Call("setGestureEnabled", appliedGestureEnabled);
                appliedInferenceBackend=InferenceBackend;
                bridge.Call("setInferenceDelegate",appliedInferenceBackend.ToString());
                appliedExposureRevision = exposureRevision;
                if (externalCamera && exposureRevision > 0)
                    bridge.Call("setExposureConfig", exposureMode, exposureTimeUs);
                bridge.Call("start");
            }
            if (externalCamera && appliedExposureRevision != exposureRevision)
            {
                appliedExposureRevision = exposureRevision;
                bridge.Call("setExposureConfig", exposureMode, exposureTimeUs);
            }
            if (appliedGestureEnabled != GestureRecognitionEnabled)
            {
                appliedGestureEnabled = GestureRecognitionEnabled;
                bridge.Call("setGestureEnabled", appliedGestureEnabled);
                Latest = null;
                lastSequence = -1;
                Invalidated?.Invoke("gesture recognition changed");
            }
            if(appliedInferenceBackend!=InferenceBackend)
            {
                appliedInferenceBackend=InferenceBackend;
                bridge.Call("setInferenceDelegate",appliedInferenceBackend.ToString());
                Latest=null;lastSequence=-1;
                Invalidated?.Invoke("inference backend changed");
            }
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 0.016f;
            State = bridge.Call<string>("getState");
            if (externalCamera)
            {
                CaptureFps = bridge.Call<float>("getCaptureFps");
                var sequence = bridge.Call<long>("getPreviewSequence");
                if (sequence > 0 && sequence != previewSequence)
                {
                    var signedBytes = bridge.Call<sbyte[]>("getPreviewJpeg");
                    if (signedBytes != null && signedBytes.Length > 0)
                    {
                        var bytes = new byte[signedBytes.Length];
                        Buffer.BlockCopy(signedBytes, 0, bytes, 0, bytes.Length);
                        if (PreviewTexture == null) PreviewTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                        PreviewTexture.LoadImage(bytes);
                        previewSequence = sequence;
                        previewCount++;
                    }
                }
                if (Time.unscaledTime - previewWindowStart >= 1f)
                {
                    PreviewFps = previewCount / (Time.unscaledTime - previewWindowStart);
                    previewCount = 0; previewWindowStart = Time.unscaledTime;
                }
            }
            var json = bridge.Call<string>("getLatestJson");
            if (string.IsNullOrEmpty(json)) return;
            var frame = JsonUtility.FromJson<SpatialHandFrame>(json);
            if (frame == null || frame.sequence == lastSequence) return;
            lastSequence = frame.sequence;
            frame.receivedHostStopwatchNs=(long)(System.Diagnostics.Stopwatch.GetTimestamp()*(1_000_000_000.0/System.Diagnostics.Stopwatch.Frequency));
            if(frame.receivedAtNs>0)
                using(var clock=new AndroidJavaClass("android.os.SystemClock"))
                {
                    long resultAge=clock.CallStatic<long>("elapsedRealtimeNanos")-frame.receivedAtNs;
                    frame.resultAgeAtReceiveMs=resultAge>=0?resultAge/1_000_000f:-1;
                }
            Latest = frame;
            FrameUpdated?.Invoke(frame);
        }

        private bool permissionRequested;
        private void OnApplicationPause(bool pause)
        {
            paused = pause;
            if (pause) StopBridge();
        }
        private void OnDisable() { StopBridge(); }
        private void OnDestroy() { StopBridge(); }
        private void StopBridge()
        {
            try { if(bridge!=null)bridge.Call("stop"); }
            finally
            {
                if(bridge!=null)bridge.Dispose();bridge=null;
                if(cameraOwner==this)cameraOwner=null;
                State="stopped";
                Invalidated?.Invoke("camera stopped/paused/disabled");
            }
            Latest = null;
            CaptureFps=PreviewFps=0;
            lastSequence = -1;
            previewSequence = -1;
            if (PreviewTexture != null) { Destroy(PreviewTexture); PreviewTexture = null; }
        }
#else
        private void Start() { State = "Android camera only"; }
#endif
    }
}

using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace SpatialAI.Unity
{
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
        public SpatialHand[] hands;
    }

    /// <summary>Image-space hands from the Android RGB camera. No metric head-space pose is claimed.</summary>
    public sealed class SpatialHandTracking : MonoBehaviour
    {
        public SpatialHandFrame Latest { get; private set; }
        public string State { get; private set; } = "idle";
        public event Action<SpatialHandFrame> FrameUpdated;
        public Texture2D PreviewTexture { get; private set; }
        public float CaptureFps { get; private set; }
        public float PreviewFps { get; private set; }

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject bridge;
        private float nextPoll;
        private long lastSequence = -1;
        private bool paused;
        private bool externalCamera;
        private long previewSequence = -1;
        private float previewWindowStart;
        private int previewCount;

        private void Update()
        {
            if (paused) return;
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
                bridge.Call("start");
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
            if (bridge == null) return;
            bridge.Call("stop");
            bridge.Dispose();
            bridge = null;
            Latest = null;
            lastSequence = -1;
            previewSequence = -1;
            if (PreviewTexture != null) { Destroy(PreviewTexture); PreviewTexture = null; }
        }
#else
        private void Start() { State = "Android camera only"; }
#endif
    }
}

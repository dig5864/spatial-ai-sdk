using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace SpatialAI.Unity
{
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

        public bool Pinching
        {
            get
            {
                if (joints == null || joints.Length < 9) return false;
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
        public long sequence;
        public long timestampNs;
        public long receivedAtNs;
        public SpatialHand[] hands;
    }

    /// <summary>Image-space hands from the Android RGB camera. No metric head-space pose is claimed.</summary>
    public sealed class SpatialHandTracking : MonoBehaviour
    {
        public SpatialHandFrame Latest { get; private set; }
        public string State { get; private set; } = "idle";
        public event Action<SpatialHandFrame> FrameUpdated;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject bridge;
        private float nextPoll;
        private long lastSequence = -1;
        private bool paused;

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
                bridge.Call("start");
            }
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 0.05f;
            State = bridge.Call<string>("getState");
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
        }
#else
        private void Start() { State = "Android camera only"; }
#endif
    }
}

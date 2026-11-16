using System;
using UnityEngine;

namespace SpatialAI.Unity
{
    /// <summary>Experimental image-relative tracking. CameraMount must be a calibrated child of the XR head.</summary>
    public sealed class SpatialImageTracking : MonoBehaviour
    {
        [Tooltip("UVC camera pose in the XR head hierarchy. Supply the measured camera-to-head transform.")]
        public Transform CameraMount;
        [Tooltip("The displayed object, parented under the camera mount while tracking.")]
        public Transform TargetAnchor;
        [Min(0.01f)] public float MaximumFrameAgeSeconds = 0.3f;
        public string State { get; private set; } = "stopped";
        public int NativeStatus { get; private set; }
        public long Sequence { get; private set; }
        public string TargetId { get; private set; }
        public string TargetVersion { get; private set; }
        public float TargetWidthMeters { get; private set; }
        public float TargetAspectRatio { get; private set; }
        [Serializable] private sealed class Sample
        {
            public int status;
            public long sequence, receiptTimestampNs;
            public string targetId, targetVersion;
            public float widthM, aspectRatio;
            public float[] cameraFromTargetGl;
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject bridge;
        private AndroidJavaClass clock;
#endif
        private bool active;

        /// <param name="intrinsics">fx, fy, cx, cy and eight distortion values for the selected input mode.</param>
        public void StartTracking(string license, string localImagePath, float widthM, float[] intrinsics,
            int sizeTier = 2, int fps = 30, int exposureMode = -1, int exposureTimeUs = -1,
            string secondImagePath = null, float secondWidthM = 0)
        {
            if (active) throw new InvalidOperationException("Image tracking already active");
            if (CameraMount == null || TargetAnchor == null || CameraMount == TargetAnchor || transform.IsChildOf(TargetAnchor) ||
                string.IsNullOrEmpty(license) || string.IsNullOrEmpty(localImagePath) || widthM <= 0 ||
                intrinsics == null || intrinsics.Length != 12 || intrinsics[0] <= 0 || intrinsics[1] <= 0 ||
                (secondImagePath != null && (secondImagePath.Length == 0 || secondWidthM <= 0 || secondImagePath == localImagePath)))
                throw new ArgumentException("Camera mount, target anchor, image, license and calibration are required");
            Hide();
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
                {
                    UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
                    throw new InvalidOperationException("Camera permission requested; retry after the user grants it");
                }
                if (bridge == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        bridge = new AndroidJavaObject("com.spatialai.vuforia.SpatialVuforiaBridge",
                            player.GetStatic<AndroidJavaObject>("currentActivity"));
                    clock = new AndroidJavaClass("android.os.SystemClock");
                }
                if (secondImagePath == null)
                    bridge.Call("start", license, localImagePath, widthM, sizeTier, fps, exposureMode, exposureTimeUs, intrinsics);
                else
                    bridge.Call("start", license, localImagePath, widthM, sizeTier, fps, exposureMode, exposureTimeUs,
                        intrinsics, secondImagePath, secondWidthM);
                active = true; State = "initializing";
            }
            catch (Exception e) { State = "error: " + e.Message; throw; }
#else
            State = "unsupported platform";
            throw new PlatformNotSupportedException("Vuforia Native tracking requires Android device build");
#endif
        }

        public void StopTracking()
        {
            active = false; Hide();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (bridge != null) bridge.Call("stop");
#endif
            State = "stopped"; NativeStatus = 0; TargetId = null; TargetVersion = null;
            TargetWidthMeters = 0; TargetAspectRatio = 0;
        }

        private void Update()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!active || bridge == null) return;
            try
            {
                State = bridge.Call<string>("getState");
                string json = bridge.Call<string>("getLatestJson");
                if (string.IsNullOrEmpty(json)) { Hide(); return; }
                Sample sample = JsonUtility.FromJson<Sample>(json);
                if (sample != null) {
                    TargetId = sample.targetId; TargetVersion = sample.targetVersion;
                    TargetWidthMeters = sample.widthM; TargetAspectRatio = sample.aspectRatio;
                }
                long now = clock.CallStatic<long>("elapsedRealtimeNanos");
                if (sample == null || sample.status != 3 || sample.receiptTimestampNs <= 0 ||
                    now < sample.receiptTimestampNs || (now - sample.receiptTimestampNs) * 1e-9 > MaximumFrameAgeSeconds ||
                    sample.cameraFromTargetGl == null || sample.cameraFromTargetGl.Length != 16)
                { NativeStatus = sample == null ? 0 : sample.status; Hide(); return; }
                Matrix4x4 gl = new Matrix4x4();
                for (int c = 0; c < 4; c++) for (int r = 0; r < 4; r++) gl[r,c] = sample.cameraFromTargetGl[c*4+r];
                Matrix4x4 flip = Matrix4x4.Scale(new Vector3(1,1,-1));
                Matrix4x4 pose = flip * gl * flip;
                Vector3 position = pose.GetColumn(3), forward = pose.GetColumn(2), up = pose.GetColumn(1);
                if (!Finite(position) || !Finite(forward) || !Finite(up) || forward.sqrMagnitude < 0.5f || up.sqrMagnitude < 0.5f)
                { Hide(); return; }
                if (TargetAnchor.parent != CameraMount) TargetAnchor.SetParent(CameraMount, false);
                TargetAnchor.SetLocalPositionAndRotation(position, Quaternion.LookRotation(forward, up));
                TargetAnchor.gameObject.SetActive(true);
                NativeStatus = sample.status; Sequence = sample.sequence;
            }
            catch (Exception e) { State = "error: " + e.Message; Hide(); }
#endif
        }
        private static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
            float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        private void Hide() { if (TargetAnchor != null) TargetAnchor.gameObject.SetActive(false); }
        private void OnApplicationPause(bool paused) { if (paused && active) StopTracking(); }
        private void OnDisable() { if (active) StopTracking(); }
        private void OnDestroy()
        {
            if (active) StopTracking();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (bridge != null) { bridge.Call("dispose"); bridge.Dispose(); }
            clock?.Dispose();
#endif
        }
    }
}

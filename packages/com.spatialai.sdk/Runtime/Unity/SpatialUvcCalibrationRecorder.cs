using System;
using System.IO;
using UnityEngine;

namespace SpatialAI.Unity
{
    /// <summary>Saves the unwarped full-field Android preview, not the reflected display.</summary>
    [RequireComponent(typeof(SpatialHandTracking))]
    public sealed class SpatialUvcCalibrationRecorder : MonoBehaviour
    {
        [Serializable]
        public sealed class CaptureMetadata
        {
            public int schemaVersion = 1;
            public string cameraId, capturedUtc, imageFile;
            public int imageWidth, imageHeight, nativeWidth, nativeHeight;
            public long previewSequence;
            public bool fullFieldOfView = true;
            public int imageRotationClockwise;
            public bool inputMirrored;
            public string source = "SpatialHandBridge full-frame 384-wide JPEG80 preview decoded to PNG";
        }
        public string OutputDirectory { get; private set; }
        public string Status { get; private set; } = "Waiting for UVC preview";
        public int SavedCount { get; private set; }
        private SpatialHandTracking tracking;
        private long savedSequence = -1;
        private float lastResultTime = float.NegativeInfinity;
        private string session;

        private void Awake()
        {
            tracking = GetComponent<SpatialHandTracking>();
            session = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            OutputDirectory = Path.Combine(Application.persistentDataPath, "uvc-calibration", session);
        }
        private void OnEnable() { tracking.FrameUpdated += OnFrame; }
        private void OnDisable() { tracking.FrameUpdated -= OnFrame; }
        private void OnFrame(SpatialHandFrame frame) { lastResultTime = Time.unscaledTime; }
        public bool Capture()
        {
            var image = tracking.PreviewTexture;
            var frame = tracking.Latest;
            if (image == null || frame == null || frame.cameraId == null || !frame.cameraId.StartsWith("uvc:") ||
                Time.unscaledTime - lastResultTime > 2 || frame.imageWidth <= 0 || frame.imageHeight <= 0 ||
                tracking.PreviewSequence < 0 || tracking.PreviewSequence == savedSequence)
            { Status = "No fresh UVC preview/result. Keep recognition enabled; move board between captures."; return false; }
            if (Math.Abs((double)image.width / image.height - (double)frame.imageWidth / frame.imageHeight) > 0.001)
            { Status = "Preview/native aspect mismatch; cannot assume a full-frame scale."; return false; }
            string pngPath = null;
            try
            {
                Directory.CreateDirectory(OutputDirectory);
                string name = "board-" + (SavedCount + 1).ToString("D3") + ".png";
                pngPath = Path.Combine(OutputDirectory, name);
                File.WriteAllBytes(pngPath, image.EncodeToPNG());
                var metadata = new CaptureMetadata { cameraId = frame.cameraId, capturedUtc = DateTime.UtcNow.ToString("O"),
                    imageFile = name, imageWidth = image.width, imageHeight = image.height,
                    nativeWidth = frame.imageWidth, nativeHeight = frame.imageHeight, previewSequence = tracking.PreviewSequence };
                File.WriteAllText(Path.ChangeExtension(pngPath, ".json"), JsonUtility.ToJson(metadata, true));
                savedSequence = tracking.PreviewSequence;
                SavedCount++;
                Status = "Saved " + name + " (preview " + image.width + "x" + image.height + ", native " + frame.imageWidth + "x" + frame.imageHeight + ")";
                Debug.Log("SpatialCalibration: " + Status + " path=" + OutputDirectory);
                return true;
            }
            catch (Exception error)
            {
                if (pngPath != null && File.Exists(pngPath)) File.Delete(pngPath);
                Status = "Save failed: " + error.Message; return false;
            }
        }
        private void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.height / 600f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            GUI.Box(new Rect(8, 8, width - 16, 145), "UVC calibration capture — full-field preview");
            if (GUI.Button(new Rect(20, 40, 200, 55), "Capture board (" + SavedCount + ")")) Capture();
            GUI.Label(new Rect(230, 40, width - 250, 60), "20–30 different views; hold still, keep all corners visible.\nDo not use a headset screenshot for calibration.");
            GUI.Label(new Rect(20, 100, width - 40, 24), Status);
            GUI.Label(new Rect(20, 125, width - 40, 24), tracking.State + "  Capture FPS: " + tracking.CaptureFps.ToString("F1"));
            if (tracking.PreviewTexture != null)
                GUI.DrawTexture(new Rect(20, 165, width - 40, height - 185), tracking.PreviewTexture, ScaleMode.ScaleToFit);
        }
    }
}

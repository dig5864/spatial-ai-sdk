using System;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

namespace SpatialAI.Unity
{
    /// <summary>Android system file picker; selected bytes are copied into the calibration store.</summary>
    public sealed class SpatialUvcCalibrationPicker : MonoBehaviour
    {
        private static SpatialUvcCalibrationPicker instance;
        public static string Status { get; private set; } = "";
        public static event Action<string> Completed;
        private bool pending;
        public static void SelectFile()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (instance == null) {
                var host = new GameObject("SpatialUvcCalibrationPicker");
                DontDestroyOnLoad(host); instance = host.AddComponent<SpatialUvcCalibrationPicker>();
            }
            if (instance.pending) { Status = "A file picker is already open."; return; }
            try {
                instance.pending = true; Status = "Choose a calibration JSON file.";
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var picker = new AndroidJavaClass("com.spatialai.calibration.CalibrationPickerActivity"))
                    picker.CallStatic("open", activity, instance.gameObject.name);
            } catch (Exception e) { instance.pending = false; Finish("Could not open file picker: " + e.Message); }
#else
            Finish("The system file picker is available in Android builds.");
#endif
        }
        [Preserve] public void OnCalibrationPickerResult(string result)
        {
            pending = false;
            try {
                if (result == "cancel") { Finish("File selection cancelled."); return; }
                if (result == null || !result.StartsWith("ok:", StringComparison.Ordinal))
                    throw new InvalidOperationException(result ?? "File picker returned no result.");
                var bytes = Convert.FromBase64String(result.Substring(3));
                if (bytes.Length == 0 || bytes.Length > SpatialUvcCalibrationStore.MaxBytes) throw new ArgumentException("File is empty or too large.");
                var json = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
                SpatialUvcCalibrationStore.SaveSelected(json);
                Finish("Calibration selected and saved.");
            } catch (Exception e) { Finish("Calibration import failed: " + e.Message); }
        }
        private static void Finish(string status)
        {
            Status = status; Debug.Log("Spatial calibration: " + status);
            if (Completed != null) Completed(status);
        }
    }
}

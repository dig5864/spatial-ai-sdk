using System;
using SpatialAI.Unity;
using UnityEngine;

namespace SpatialAI.XRHands
{
    /// <summary>Optional basic UI; custom Unity UI can call the same public methods.</summary>
    public sealed class SpatialUvcCalibrationUI : MonoBehaviour
    {
        public bool visible = true;
        private string status = "";
        private const string FlipKey = "SpatialAI.UvcHandDisplayFlipX";
        private static int? initialFlip;
        public static bool FlipX
        {
            get
            {
                if (!initialFlip.HasValue)
                    initialFlip = System.IO.File.Exists(System.IO.Path.Combine(SpatialUvcCalibrationStore.DirectoryPath, "flip-hand-display-x")) ? 1 : 0;
                return PlayerPrefs.GetInt(FlipKey, initialFlip.Value) != 0;
            }
        }
        public static void SetFlipX(bool value)
        {
            PlayerPrefs.SetInt(FlipKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
        public void ToggleFlipX() { SetFlipX(!FlipX); status = "Hand display horizontal flip: " + (FlipX ? "on" : "off"); }
        private void OnEnable() { SpatialUvcCalibrationPicker.Completed += OnCompleted; }
        private void OnDisable() { SpatialUvcCalibrationPicker.Completed -= OnCompleted; }
        private void OnCompleted(string message) { status = message; }
        public void SelectCalibration() { SpatialUvcCalibrationPicker.SelectFile(); }
        public void UseDefaultCalibration()
        {
            try { SpatialUvcCalibrationStore.ClearSelected(); status = "Using default file or packaged calibration."; }
            catch (Exception e) { status = "Could not reset selection: " + e.Message; }
        }
        public void ReloadCalibration()
        {
            try { SpatialUvcCalibrationStore.Reload(); status = "Reload requested."; }
            catch (Exception e) { status = "Reload failed: " + e.Message; }
        }
        private void OnGUI()
        {
            if (!visible) return;
            GUILayout.BeginArea(new Rect(12, 12, Math.Min(Screen.width - 24, 580), 270), GUI.skin.box);
            GUILayout.Label("UVC calibration");
            if (GUILayout.Button("Select calibration JSON")) SelectCalibration();
            if (GUILayout.Button("Use default / packaged calibration")) UseDefaultCalibration();
            if (GUILayout.Button("Reload calibration")) ReloadCalibration();
            if (GUILayout.Button("Flip hand display left / right: " + (FlipX ? "ON" : "OFF"))) ToggleFlipX();
            GUILayout.Label(status);
            var driver = GetComponent<SpatialUvcHands>();
            if (driver != null) GUILayout.Label(driver.Status);
            GUILayout.EndArea();
        }
    }
}

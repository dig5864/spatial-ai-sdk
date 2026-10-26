#if SPATIALAI_HAS_CARDBOARD
using System;
using System.Globalization;
using SpatialAI.Cardboard;
using UnityEngine;

namespace SpatialAI.Samples
{
    // Demo preferences and UI only. Frame-rate calculation/application belongs to the SDK.
    public sealed class SpatialFrameRateDebugControls : MonoBehaviour
    {
        private const string ModePreference = "SpatialAI.Demo.FrameRateMode";
        private const string HeadroomPreference = "SpatialAI.Demo.FrameRateHeadroom";
        private SpatialScreenCalibration settings;
        private float nextLookup;
        private string error;
        private GUIStyle title, label, button;

        private void Awake()
        {
            if (!PlayerPrefs.HasKey(ModePreference))
            {
                SpatialFrameRate.Configure(SpatialFrameRateConfiguration.Default);
                return;
            }
            try
            {
                SpatialFrameRate.Configure((SpatialFrameRateMode)PlayerPrefs.GetInt(ModePreference),
                    PlayerPrefs.GetFloat(HeadroomPreference, 2f));
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning("Spatial sample frame-rate preference ignored: " + exception.Message);
                PlayerPrefs.DeleteKey(ModePreference); PlayerPrefs.DeleteKey(HeadroomPreference);
                SpatialFrameRate.Configure(SpatialFrameRateConfiguration.Default);
            }
        }

        public void Configure(string value)
        {
            var parts = (value ?? "").Split('|');
            var current = SpatialFrameRate.Configuration;
            var mode = current.mode;
            var margin = current.softwareHeadroomMultiplier;
            if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]) && !Enum.TryParse(parts[0], true, out mode))
            { Reject("Use FollowDisplay or Locked60."); return; }
            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) &&
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out margin))
            { Reject("Invalid software headroom."); return; }
            Apply(mode, margin, "broadcast");
        }

        private void Reject(string message) { error = message; Debug.LogWarning("Spatial sample frame-rate rejected: " + message); }
        private void Apply(SpatialFrameRateMode mode, float margin, string source)
        {
            try
            {
                SpatialFrameRate.Configure(mode, margin);
                PlayerPrefs.SetInt(ModePreference, (int)mode);
                PlayerPrefs.SetFloat(HeadroomPreference, margin); PlayerPrefs.Save();
                error = null;
                Debug.Log("Spatial sample frame-rate requested through SDK: mode=" + mode + " headroom=" + margin + " source=" + source);
            }
            catch (ArgumentException exception) { Reject(exception.Message); }
        }

        private void Update()
        {
            if (Time.unscaledTime < nextLookup) return;
            nextLookup = Time.unscaledTime + .5f;
            if (settings == null) settings = FindObjectOfType<SpatialScreenCalibration>();
        }

        public void ShowSettings(bool visible)
        {
            if (settings == null) settings = FindObjectOfType<SpatialScreenCalibration>();
            if (settings == null) return;
            if (visible) settings.Open(); else settings.Close();
        }

        private void OnGUI()
        {
            if (settings == null || !settings.IsOpen) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, wordWrap = true };
                label = new GUIStyle(GUI.skin.label) { fontSize = 23, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { fontSize = 23, wordWrap = true };
            }
            float scale = Mathf.Min(Screen.height / 1080f, Screen.width / 1800f);
            if (scale <= 0) return;
            float width = Mathf.Min(620, (Screen.width / scale - 1000) * .5f - 40);
            var previous = GUI.matrix;
            // Leave the physical rulers and Android's optional refresh overlay visible.
            GUI.matrix = Matrix4x4.TRS(new Vector3(100 * scale, 130 * scale, 0), Quaternion.identity,
                new Vector3(scale, scale, 1));
            try
            {
                GUI.Box(new Rect(20, 90, width, 610), "");
                var config = SpatialFrameRate.Configuration;
                var status = SpatialFrameRate.Status;
                GUI.Label(new Rect(40, 110, width - 40, 50), "Frame rate (SDK)", title);
                if (GUI.Button(new Rect(40, 170, width - 40, 65),
                    (config.mode == SpatialFrameRateMode.Locked60 ? "* " : "") + "Lock 60 FPS", button))
                    Apply(SpatialFrameRateMode.Locked60, config.softwareHeadroomMultiplier, "gear");
                if (GUI.Button(new Rect(40, 245, width - 40, 65),
                    (config.mode == SpatialFrameRateMode.FollowDisplay ? "* " : "") + "Follow screen refresh", button))
                    Apply(SpatialFrameRateMode.FollowDisplay, config.softwareHeadroomMultiplier, "gear");
                GUI.Label(new Rect(40, 325, width - 40, 45), "Software headroom", label);
                float cell = (width - 60) / 3;
                for (int i = 0; i < 3; i++)
                {
                    float margin = i + 2;
                    if (GUI.Button(new Rect(40 + i * (cell + 10), 375, cell, 55),
                        (Mathf.Approximately(config.softwareHeadroomMultiplier, margin) ? "* " : "") + margin + "x", button))
                        Apply(config.mode, margin, "gear");
                }
                bool applied = status.IsActive && status.ConfigurationVersion == SpatialFrameRate.ConfigurationVersion;
                string text = applied ? string.Format(CultureInfo.InvariantCulture,
                    "Screen: {0:F2} Hz\nTarget: {1:F2} FPS\nSoftware cap: {2}\nActual FPS: see IMU logs",
                    status.DisplayRefreshHz, status.TargetFrameRate, status.SoftwareFrameRateCap) : "Waiting for SDK to apply...";
                GUI.Label(new Rect(40, 445, width - 40, 155), text, label);
                GUI.Label(new Rect(40, 610, width - 40, 75), error ?? "Saved for this demo. Close settings to compare motion.", label);
            }
            finally { GUI.matrix = previous; }
        }
    }
}
#endif

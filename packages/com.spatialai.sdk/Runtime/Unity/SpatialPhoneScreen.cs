using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SpatialAI.Core;
using UnityEngine;

namespace SpatialAI.Unity
{
    public sealed class PhoneScreenMetrics
    {
        public int widthPx, heightPx, displayWidthPx, displayHeightPx, rotation, displayId, modeId;
        public int modeWidthPx, modeHeightPx, windowWidthPx, windowHeightPx;
        public float xdpi, ydpi;
        public string phoneKey, diagnostic;
        public bool fullDisplayMapping;
        public PhoneScreenCalibration estimated;
    }

    /// <summary>Physical dimensions are independent of the current output resolution.
    /// Android reported DPI is an estimate, never a measurement certification.</summary>
    public static class SpatialPhoneScreen
    {
        public static int Revision { get; private set; }
        public static TextAsset ConfiguredRig { get; set; }
        public static string DirectoryPath { get { return Path.Combine(Application.persistentDataPath, "spatial-screen"); } }
        public static string RigPath { get { return Path.Combine(DirectoryPath, "rig.json"); } }
        private static string PhonePath(string key)
        {
            using (var sha = SHA256.Create())
                return Path.Combine(DirectoryPath, "phone-" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "") + ".json");
        }
        public static PhoneScreenMetrics Read()
        {
            var identity = SpatialDeviceProfiles.ReadHostIdentity();
            var value = new PhoneScreenMetrics { widthPx = Screen.width, heightPx = Screen.height,
                phoneKey = identity.Manufacturer + "/" + identity.Model + "/" + identity.DeviceCode,
                diagnostic = "Android fullscreen LandscapeLeft is required." };
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                using (var manager = activity.Call<AndroidJavaObject>("getWindowManager"))
                using (var display = version.GetStatic<int>("SDK_INT") >= 30 ?
                    activity.Call<AndroidJavaObject>("getDisplay") : manager.Call<AndroidJavaObject>("getDefaultDisplay"))
                using (var metrics = new AndroidJavaObject("android.util.DisplayMetrics"))
                {
                    display.Call("getRealMetrics", metrics);
                    value.displayWidthPx = metrics.Get<int>("widthPixels");
                    value.displayHeightPx = metrics.Get<int>("heightPixels");
                    value.xdpi = metrics.Get<float>("xdpi"); value.ydpi = metrics.Get<float>("ydpi");
                    value.rotation = display.Call<int>("getRotation"); value.displayId = display.Call<int>("getDisplayId");
                    using (var mode = display.Call<AndroidJavaObject>("getMode"))
                    {
                        value.modeId = mode.Call<int>("getModeId");
                        value.modeWidthPx = mode.Call<int>("getPhysicalWidth"); value.modeHeightPx = mode.Call<int>("getPhysicalHeight");
                    }
                    bool multiWindow = activity.Call<bool>("isInMultiWindowMode");
                    bool windowMatches = true;
                    if (version.GetStatic<int>("SDK_INT") >= 30)
                    {
                        using (var window = manager.Call<AndroidJavaObject>("getCurrentWindowMetrics"))
                        using (var bounds = window.Call<AndroidJavaObject>("getBounds"))
                        {
                            value.windowWidthPx = bounds.Call<int>("width"); value.windowHeightPx = bounds.Call<int>("height");
                            windowMatches = value.windowWidthPx == value.displayWidthPx && value.windowHeightPx == value.displayHeightPx;
                        }
                    }
                    // Reject cropped windows and unknown output aspect mappings.
                    // Uniform full-display downsampling remains supported.
                    value.fullDisplayMapping = !multiWindow && windowMatches && Screen.fullScreen && Screen.orientation == ScreenOrientation.LandscapeLeft &&
                        value.displayWidthPx > value.displayHeightPx && value.widthPx > value.heightPx && value.heightPx > 0 &&
                        Math.Abs((double)value.widthPx / value.heightPx - (double)value.displayWidthPx / value.displayHeightPx) < .005;
                    value.diagnostic = value.fullDisplayMapping ? "Full-display output; Android DPI estimate." : "Output/display mapping mismatch; enable fullscreen LandscapeLeft.";
                    if (ScreenLayout.Positive(value.xdpi) && ScreenLayout.Positive(value.ydpi))
                    {
                        float w = value.displayWidthPx / value.xdpi * 25.4f, h = value.displayHeightPx / value.ydpi * 25.4f;
                        // Broad phone-sized bounds only reject grossly invalid reports.
                        if (w >= 80 && w <= 250 && h >= 35 && h <= 150 && w > h)
                            value.estimated = new PhoneScreenCalibration { phoneKey = value.phoneKey, widthMm = w, heightMm = h };
                    }
                }
            }
            catch (Exception e) { value.diagnostic = "Screen metrics unavailable: " + e.Message; }
#endif
            return value;
        }
        public static PhoneScreenCalibration Resolve(PhoneScreenMetrics metrics)
        {
            if (metrics == null) return null;
            try
            {
                string path = PhonePath(metrics.phoneKey);
                if (File.Exists(path))
                {
                    var saved = JsonUtility.FromJson<PhoneScreenCalibration>(File.ReadAllText(path));
                    if (saved != null && saved.IsValid && saved.phoneKey == metrics.phoneKey) return saved;
                }
            }
            catch (Exception e) { Debug.LogWarning("Spatial screen calibration: " + e.Message); }
            return metrics.estimated;
        }
        public static ScreenRigProfile LoadRig(TextAsset configured = null)
        {
            // Explicit application configuration takes precedence over local cache.
            try
            {
                var asset = configured != null ? configured : ConfiguredRig != null ? ConfiguredRig : Resources.Load<TextAsset>("SpatialAI/ScreenRig");
                string json = asset != null ? asset.text : File.Exists(RigPath) ? File.ReadAllText(RigPath) : null;
                var rig = json == null ? null : JsonUtility.FromJson<ScreenRigProfile>(json);
                return rig != null && rig.IsValid ? rig : null;
            }
            catch (Exception e) { Debug.LogWarning("Spatial screen rig: " + e.Message); return null; }
        }
        public static void Save(PhoneScreenCalibration calibration)
        {
            if (calibration == null || !calibration.IsValid) throw new ArgumentException("Invalid screen calibration.");
            calibration.savedUtc = DateTime.UtcNow.ToString("O");
            Write(PhonePath(calibration.phoneKey), JsonUtility.ToJson(calibration, true)); Revision++;
        }
        public static void SaveRig(ScreenRigProfile rig)
        {
            if (rig == null || !rig.IsValid) throw new ArgumentException("Invalid screen rig.");
            Write(RigPath, JsonUtility.ToJson(rig, true)); Revision++;
        }
        private static void Write(string path, string json)
        {
            Directory.CreateDirectory(DirectoryPath);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, json);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }
}

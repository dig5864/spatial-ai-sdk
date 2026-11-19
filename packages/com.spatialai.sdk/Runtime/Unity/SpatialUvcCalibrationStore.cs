using System;
using System.IO;
using System.Text;
using SpatialAI.Core;
using UnityEngine;

namespace SpatialAI.Unity
{
    /// <summary>Android UVC calibration shared by hand and image tracking.</summary>
    public static class SpatialUvcCalibrationStore
    {
        public const int MaxBytes = 1024 * 1024;
        public static string DirectoryPath { get { return Path.Combine(Application.persistentDataPath, "spatial-calibration"); } }
        /// <summary>Legacy file location retained for callers that manage older installations.</summary>
        public static string DefaultPath { get { return Path.Combine(DirectoryPath, "uvc-calibration.json"); } }
        private static string SelectionDirectory { get { return Path.Combine(DirectoryPath, "selected"); } }
        private static string SelectedPath
        {
            get
            {
                if (!Directory.Exists(SelectionDirectory)) return null;
                var files = Directory.GetFiles(SelectionDirectory, "*.json");
                Array.Sort(files, StringComparer.Ordinal);
                return files.Length == 0 ? null : files[files.Length - 1];
            }
        }
        public static bool HasSelected { get { return SelectedPath != null; } }
        public static event Action Changed;

        /// <summary>Loads an explicitly selected calibration or the Android diagnostics default. The asset is used for non-Android preview.</summary>
        public static UvcHandCalibration Load(TextAsset packaged, out string source)
        {
            string selected = SelectedPath;
            if (selected != null)
            {
                source = "selected";
                return ParseActive(ReadFile(selected));
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            source = "shared calibration";
            return LoadSharedDefault();
#else
            source = packaged == null ? "none" : "packaged";
            return packaged == null ? null : ParseActive(packaged.text);
#endif
        }

        /// <summary>Loads and validates the Android provider's default directly.</summary>
        public static UvcHandCalibration LoadSharedDefault()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                string json = ReadSharedDefaultJson();
                if (json == null) throw new IOException("The diagnostics calibration provider has no shared default.");
                return ParseActive(json, shared: true);
            }
            catch (Exception error) when (error is AndroidJavaException || error is IOException ||
                                          error is ArgumentException || error is FormatException)
            {
                throw new InvalidOperationException("Could not load shared UVC calibration: " + error.Message, error);
            }
#else
            throw new PlatformNotSupportedException("Shared UVC calibration requires an Android player.");
#endif
        }

        /// <summary>Reads the Android diagnostics provider's original JSON, including optional display and eye alignment.</summary>
        /// <returns>The original UTF-8 JSON as text, or null if unavailable or outside an Android player.</returns>
        public static string ReadSharedDefaultJson()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var reader = new AndroidJavaClass("com.spatialai.calibration.CalibrationPickerActivity"))
            {
                if (activity == null) return null;
                string encoded = reader.CallStatic<string>("readSharedDefault", activity);
                return encoded == null ? null : new UTF8Encoding(false, true).GetString(Convert.FromBase64String(encoded));
            }
#else
            return null;
#endif
        }

        private static UvcHandCalibration ParseActive(string json, bool shared = false)
        {
            var calibration = ParseExternal(json, out var mountRotation, out var swapHandedness);
            if (shared && !mountRotation.HasValue)
                throw new ArgumentException("The shared default must be a VISOR-X UVC calibration bundle.");
            int sizeTier = shared ? SpatialHandTracking.GetUvcSizeTier(calibration.imageWidth, calibration.imageHeight) : 0;
            if (mountRotation.HasValue)
            {
                // The bundle describes raw UVC pixels. Deliver oriented landmarks and
                // let the converter undo that transform before unprojecting them.
                calibration.imageRotationClockwise = mountRotation.Value;
                calibration.inputMirrored = SpatialHandTracking.UvcMirrorOutputX;
                calibration.swapHandedness = swapHandedness ?? SpatialHandTracking.UvcSwapHandednessOverride ??
                    (SpatialDeviceProfiles.SelectHost(SpatialDeviceProfiles.ReadHostIdentity(), out var host).Success && host.uvcSwapHandedness);
                SpatialHandTracking.UvcMountRotationDegrees = mountRotation.Value;
            }
            if (shared && SpatialHandTracking.UsesExternalUvcCamera)
                SpatialHandTracking.CameraSizeTier = sizeTier;
            return calibration;
        }

        public static UvcHandCalibration ParseExternal(string json)
        {
            return ParseExternal(json, out _, out _);
        }

        private static UvcHandCalibration ParseExternal(string json, out int? mountRotation, out bool? swapHandedness)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new ArgumentException("Calibration file is empty or too large.");
            var c = SpatialUvcCalibrationLoader.Load(json, out mountRotation, out swapHandedness);
            if (c == null || c.imageWidth <= 0 || c.imageHeight <= 0 || c.fx <= 0 || c.fy <= 0 ||
                c.distortion == null || (c.distortion.Length != 5 && c.distortion.Length != 8))
                throw new ArgumentException("Calibration must contain image dimensions, intrinsics and distortion.");
            int w = c.imageRotationClockwise % 180 == 0 ? c.imageWidth : c.imageHeight;
            int h = c.imageRotationClockwise % 180 == 0 ? c.imageHeight : c.imageWidth;
            UvcHandCalibration resolved; string warning;
            var result = UvcHandCalibration.ResolveForPreview(c, string.IsNullOrEmpty(c.cameraId) ? "uvc:unbound" : c.cameraId, w, h, out resolved, out warning);
            if (!result.Success) throw new ArgumentException("Invalid calibration: " + result.Message);
            return c;
        }
        public static void SaveSelected(string json)
        {
            ParseExternal(json); // Validate before replacing a previously selected file.
            Directory.CreateDirectory(SelectionDirectory);
            string old = SelectedPath;
            long sequence = old == null ? 0 : long.Parse(Path.GetFileNameWithoutExtension(old), System.Globalization.CultureInfo.InvariantCulture);
            string name = checked(sequence + 1).ToString("D20", System.Globalization.CultureInfo.InvariantCulture);
            string path = Path.Combine(SelectionDirectory, name + ".json");
            string temp = Path.Combine(SelectionDirectory, name + ".pending");
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            try { File.Move(temp, path); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            foreach (string previous in Directory.GetFiles(SelectionDirectory, "*.json"))
                if (previous != path) { try { File.Delete(previous); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            if (Changed != null) Changed();
        }
        public static void ClearSelected()
        {
            if (Directory.Exists(SelectionDirectory))
                foreach (string path in Directory.GetFiles(SelectionDirectory, "*.json")) File.Delete(path);
            if (Changed != null) Changed();
        }
        public static void Reload() { if (Changed != null) Changed(); }
        private static string ReadFile(string path)
        {
            var info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > MaxBytes) throw new IOException("Calibration file is empty or exceeds 1 MiB: " + path);
            return File.ReadAllText(path, new UTF8Encoding(false, true));
        }
    }
}

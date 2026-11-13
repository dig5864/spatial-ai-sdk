using System;
using System.IO;
using System.Text;
using SpatialAI.Core;
using UnityEngine;

namespace SpatialAI.Unity
{
    /// <summary>Persistent selection, default file and packaged fallback for UVC hand calibration.</summary>
    public static class SpatialUvcCalibrationStore
    {
        public const int MaxBytes = 1024 * 1024;
        public static string DirectoryPath { get { return Path.Combine(Application.persistentDataPath, "spatial-calibration"); } }
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
        public static UvcHandCalibration Load(TextAsset packaged, out string source)
        {
            Directory.CreateDirectory(DirectoryPath);
            string selected = SelectedPath;
            string path = selected ?? (File.Exists(DefaultPath) ? DefaultPath : null);
            if (path != null)
            {
                var value = ParseExternal(ReadFile(path));
                source = path == selected ? "selected" : "default file";
                return ForDeliveredFrames(value);
            }
            source = packaged == null ? "none" : "packaged";
            return packaged == null ? null : ForDeliveredFrames(SpatialUvcCalibrationLoader.Load(packaged.text));
        }
        private static UvcHandCalibration ForDeliveredFrames(UvcHandCalibration calibration)
        {
            // VISOR-X intrinsics describe raw UVC frames. The SDK may rotate and
            // mirror landmark coordinates before passing them to the converter.
            if (calibration != null && calibration.source == "VISOR-X UVC calibration bundle")
            {
                calibration.imageRotationClockwise = SpatialHandTracking.UvcMountRotationDegrees;
                calibration.inputMirrored = SpatialHandTracking.UvcMirrorOutputX;
            }
            return calibration;
        }
        public static UvcHandCalibration ParseExternal(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new ArgumentException("Calibration file is empty or too large.");
            var c = SpatialUvcCalibrationLoader.Load(json);
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

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Watches for requests from BuildAndroid.ps1 so a project already open in Unity can build safely.
[InitializeOnLoad]
public static class SpatialXRBuildAutomation
{
    [Serializable]
    private sealed class BuildRequest
    {
        public string id = "";
    }

    [Serializable]
    private sealed class BuildStatus
    {
        public string id;
        public string state;
        public string message;
        public string apkPath;
        public string logPath;
    }

    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private static readonly string SdkRoot = Path.GetFullPath(Path.Combine(ProjectRoot, "..", "..", ".."));
    private static readonly string RequestPath = Path.Combine(SdkRoot, "build", "automation", "SpatialXRBuildRequest.json");
    private static readonly string StatusPath = Path.Combine(SdkRoot, "build", "automation", "SpatialXRBuildStatus.json");
    private static readonly string ApkPath = Path.Combine(SdkRoot, "build", "samples", "android", "spatialxr.apk");
    private static DateTime nextCheck;
    private static bool building;
    private static StreamWriter logWriter;

    static SpatialXRBuildAutomation()
    {
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (building || DateTime.UtcNow < nextCheck) return;
        nextCheck = DateTime.UtcNow.AddMilliseconds(500);
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;

        BuildRequest request;
        try
        {
            request = JsonUtility.FromJson<BuildRequest>(File.ReadAllText(RequestPath));
            if (request == null || string.IsNullOrEmpty(request.id) ||
                !Regex.IsMatch(request.id, "^[0-9a-fA-F]{32}$"))
                throw new InvalidDataException("Invalid Spatial XR build request ID.");
            File.Delete(RequestPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return;
        }

        building = true;
        var logDir = Path.Combine(SdkRoot, "build", "logs");
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, "spatialxr-android-" + request.id + ".log");
        try
        {
            logWriter = new StreamWriter(logPath, false, new UTF8Encoding(false)) { AutoFlush = true };
            Application.logMessageReceived += CaptureLog;
            WriteStatus(request.id, "running", "Unity is building the Android APK.", logPath);
            Debug.Log("Spatial XR automated Android build started: " + request.id);
            SpatialXRSetup.BuildAndroidSample();
            if (!File.Exists(ApkPath)) throw new FileNotFoundException("Unity reported success but the APK is missing.", ApkPath);
            Debug.Log("Spatial XR automated Android build succeeded: " + ApkPath);
            WriteStatus(request.id, "succeeded", "Android APK generated.", logPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            WriteStatus(request.id, "failed", exception.Message, logPath);
        }
        finally
        {
            Application.logMessageReceived -= CaptureLog;
            if (logWriter != null) logWriter.Dispose();
            logWriter = null;
            building = false;
        }
    }

    private static void CaptureLog(string message, string stackTrace, LogType type)
    {
        if (logWriter == null) return;
        logWriter.WriteLine("[" + type + "] " + message);
        if ((type == LogType.Error || type == LogType.Exception) && !string.IsNullOrEmpty(stackTrace))
            logWriter.WriteLine(stackTrace);
    }

    private static void WriteStatus(string id, string state, string message, string logPath)
    {
        var status = new BuildStatus
        {
            id = id, state = state, message = message,
            apkPath = ApkPath, logPath = logPath
        };
        var temporaryPath = StatusPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonUtility.ToJson(status), new UTF8Encoding(false));
        if (File.Exists(StatusPath)) File.Delete(StatusPath);
        File.Move(temporaryPath, StatusPath);
    }
}

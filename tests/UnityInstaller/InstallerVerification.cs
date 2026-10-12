using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

[InitializeOnLoad]
public static class InstallerVerification
{
    [Serializable] private sealed class Probe { public string phase, startedUtc, manifestHash, archivePath; }
    [Serializable] private sealed class State { public string phase, error; }
    private static readonly string[] Names = { "com.spatialai.sdk", "com.spatialai.xr-hands", "com.spatialai.xr-interaction", "com.google.xr.cardboard" };
    private const string ProbePath = "installer-probe.json";
    private const string StatePath = "ProjectSettings/SpatialAISdkInstallationState.json";
    private static AddRequest entryRequest;
    private static double after;

    static InstallerVerification() { EditorApplication.update += Tick; after = EditorApplication.timeSinceStartup + 3; }

    public static void Start()
    {
        Save(new Probe { phase = "Installing", startedUtc = DateTime.UtcNow.ToString("O") });
        entryRequest = Client.Add("file:../IncomingSDK/sdk.tgz");
    }

    public static void StartLegacyUpgrade()
    {
        Save(new Probe { phase = "Legacy", startedUtc = DateTime.UtcNow.ToString("O") });
        entryRequest = Client.Add("file:../IncomingSDK/legacy-installer.tgz");
    }

    public static void WaitUntilReady()
    {
        Save(new Probe { phase = "WaitUntilReady", startedUtc = DateTime.UtcNow.ToString("O") });
    }

    public static void VerifyRelocated()
    {
        VerifyPackages();
        Check(ReadState().phase == "Completed", "Completed state survives relocation");
        Check(Hash("Packages/manifest.json") == ReadProbe().manifestHash, "Relative references survive relocation");
        Check(File.ReadAllText("Assets/KeepMe.txt") == "preserved application asset", "Application asset preserved");
        Debug.Log("SPATIALAI_INSTALLER_RELOCATION_PASS");
    }

    private static void Tick()
    {
        if (!File.Exists(ProbePath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < after) return;
        try
        {
            var probe = ReadProbe();
            if (probe.phase == "Done") return;
            if ((DateTime.UtcNow - DateTime.Parse(probe.startedUtc).ToUniversalTime()).TotalMinutes > 5)
                throw new Exception("Installer validation timed out in " + probe.phase);
            if (entryRequest != null)
            {
                if (!entryRequest.IsCompleted) return;
                if (entryRequest.Status == StatusCode.Failure) throw new Exception(entryRequest.Error.message);
                entryRequest = null;
            }
            if (probe.phase == "Legacy")
            {
                const string legacyState = "ProjectSettings/SpatialAIInstallerState.json";
                if (!File.Exists(legacyState)) return;
                var previous = JsonUtility.FromJson<State>(File.ReadAllText(legacyState));
                Check(previous.phase != "Failed", "Legacy fixture install failed: " + previous.error);
                if (previous.phase != "Completed") return;
                Check(UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.spatialai.installer") != null, "Legacy installer is present before upgrade");
                probe.phase = "Installing";
                Save(probe);
                entryRequest = Client.Add("file:../IncomingSDK/sdk.tgz");
                return;
            }
            if (!File.Exists(StatePath)) return;
            var state = ReadState();
            if (probe.phase == "WaitUntilReady")
            {
                Check(state.phase != "Failed", "SDK install failed: " + state.error);
                if (state.phase != "Completed") return;
                VerifyPackages();
                probe.phase = "Done";
                Save(probe);
                Debug.Log("SPATIALAI_INSTALLER_READY");
                EditorApplication.Exit(0);
                return;
            }
            if (probe.phase == "Installing")
            {
                Check(state.phase != "Failed", "Automatic install failed: " + state.error);
                if (state.phase != "Completed") return;
                VerifyPackages();
                Check(File.ReadAllText("Assets/KeepMe.txt") == "preserved application asset", "Application asset preserved");
                Check(File.ReadAllText("Packages/manifest.json").Contains("com.unity.modules.physics"), "Existing dependency preserved");
                int backups = File.Exists("IncomingSDK/legacy-installer.tgz") ? 2 : 1;
                Check(Directory.GetFiles("SDKPackages", "manifest-before-install.json", SearchOption.AllDirectories).Length == backups, "One manifest backup per release");
                probe.manifestHash = Hash("Packages/manifest.json");
                var sdk = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.spatialai.sdk");
                string archiveDirectory = Path.GetDirectoryName(Path.GetFullPath(Path.Combine("Packages", sdk.packageId.Substring("com.spatialai.sdk@file:".Length))));
                probe.archivePath = Directory.GetFiles(archiveDirectory, "com.spatialai.xr-hands-*.tgz").Single();
                File.Move("IncomingSDK/sdk.tgz", "original-download-moved.tgz");
                probe.phase = "Reload";
                Save(probe);
                File.WriteAllText("Assets/Editor/ReloadProbe.cs", "// Trigger a real assembly reload after completed installation.\npublic sealed class ReloadProbe {}\n");
                AssetDatabase.Refresh();
                after = EditorApplication.timeSinceStartup + 8;
                return;
            }
            if (probe.phase == "Reload")
            {
                Check(state.phase == "Completed", "Assembly reload keeps completed state");
                Check(Hash("Packages/manifest.json") == probe.manifestHash, "Reload is idempotent");
                int backups = File.Exists("IncomingSDK/legacy-installer.tgz") ? 2 : 1;
                Check(Directory.GetFiles("SDKPackages", "manifest-before-install.json", SearchOption.AllDirectories).Length == backups, "Reload creates no extra backup");
                File.Copy(probe.archivePath, "archive-before-corruption.tgz");
                File.WriteAllText(probe.archivePath, "corrupted test archive");
                probe.phase = "ExpectFailure";
                Save(probe);
                Check(EditorApplication.ExecuteMenuItem("Spatial AI/SDK Installation/Retry Installation"), "Retry menu exists");
                return;
            }
            if (probe.phase == "ExpectFailure")
            {
                if (state.phase != "Failed") return;
                Check(state.error.Contains("Existing project archive has different content"), "Corrupt archive rejected before UPM mutation");
                Check(Hash("Packages/manifest.json") == probe.manifestHash, "Failed preflight preserves manifest");
                File.Copy("archive-before-corruption.tgz", probe.archivePath, true);
                probe.phase = "Retry";
                Save(probe);
                Check(EditorApplication.ExecuteMenuItem("Spatial AI/SDK Installation/Retry Installation"), "Retry after correction");
                return;
            }
            if (probe.phase == "Retry")
            {
                // The persisted failure remains until the scheduled retry begins on the next idle update.
                if (state.phase == "Failed") return;
                if (state.phase != "Completed") return;
                VerifyPackages();
                Check(Hash("Packages/manifest.json") == probe.manifestHash, "Retry keeps correct dependencies");
                probe.phase = "Done";
                Save(probe);
                Debug.Log("SPATIALAI_INSTALLER_PASS automatic=3 reload=ok checksumFailure=ok retry=ok projectLocalEntry=ok legacyRemoved=ok");
                EditorApplication.Exit(0);
            }
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void VerifyPackages()
    {
        Check(UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.spatialai.installer") == null, "Standalone legacy installer removed");
        foreach (string name in Names)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + name);
            Check(package != null && package.source == PackageSource.LocalTarball, "Release tarball installed: " + name);
            Check(Regex.IsMatch(File.ReadAllText("Packages/manifest.json"), "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"file:\\.\\./SDKPackages/SpatialAI/"), "Project-local relative manifest reference: " + name);
            Debug.Log("SPATIALAI_INSTALLER_PACKAGE " + package.name + " " + package.version + " " + package.source);
        }
        foreach (string name in new[] { "com.unity.xr.hands", "com.unity.xr.interaction.toolkit", "com.unity.inputsystem", "com.unity.xr.management" })
            Check(UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + name) != null, "Unity dependency resolved: " + name);
    }
    private static Probe ReadProbe() => JsonUtility.FromJson<Probe>(File.ReadAllText(ProbePath));
    private static State ReadState() => JsonUtility.FromJson<State>(File.ReadAllText(StatePath));
    private static void Save(Probe probe) => File.WriteAllText(ProbePath, JsonUtility.ToJson(probe, true));
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static string Hash(string path)
    {
        using (var input = File.OpenRead(path))
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(input));
    }
}

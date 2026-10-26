using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Covers menu, batchmode and automated Android builds before packaging assets.
public sealed class SpatialXRNativeDependencies : IPreprocessBuildWithReport
{
    public int callbackOrder { get { return -1000; } }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android) return;
        if (Application.platform != RuntimePlatform.WindowsEditor)
            throw new BuildFailedException("Prepare the RhinoX Android dependencies on Windows; the current build script requires PowerShell.");

        var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.spatialai.sdk");
        if (package == null) throw new BuildFailedException("Spatial AI SDK package is missing.");
        var sdkRoot = Path.GetFullPath(Path.Combine(package.resolvedPath, "..", ".."));
        var script = Path.Combine(sdkRoot, "tools", "BuildRhinoXCardboard.ps1");
        if (!File.Exists(script)) throw new BuildFailedException("Use the repository SDK package to build this sample: " + script);
        var ndk = Environment.GetEnvironmentVariable("SPATIALAI_ANDROID_NDK_ROOT");
        if (string.IsNullOrWhiteSpace(ndk)) ndk = AndroidExternalToolsSettings.ndkRootPath;
        if (string.IsNullOrWhiteSpace(ndk)) throw new BuildFailedException("Configure the Android NDK in Unity External Tools or SPATIALAI_ANDROID_NDK_ROOT.");

        var shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");
        var arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -NdkPath \"" + ndk + "\"";
        // Rebuild the display-timed plugin instead of overwriting it with the baseline SDK.
        if (File.Exists(Path.Combine(sdkRoot, "tools/BuildUnityImuTiming.py")))
        {
            shell = "python";
            arguments = "-X utf8 \"" + Path.Combine(sdkRoot, "tools/BuildUnityImuTiming.py") + "\" --install";
        }
        var start = new ProcessStartInfo(shell, arguments)
        {
            WorkingDirectory = sdkRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        // Unity may inherit PowerShell 7 module paths, which Windows PowerShell
        // cannot load. Dependency preparation only needs its built-in modules.
        start.EnvironmentVariables["PSModulePath"] = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "Modules");
        using (var process = Process.Start(start))
        {
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            UnityEngine.Debug.Log(output.GetAwaiter().GetResult());
            var errorText = errors.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
                throw new BuildFailedException("RhinoX dependency preparation failed (" + process.ExitCode + "): " + errorText);
            if (!string.IsNullOrWhiteSpace(errorText)) UnityEngine.Debug.LogWarning(errorText);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }
}

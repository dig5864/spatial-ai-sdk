using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;

// Unity 2022 generates Gradle files before calling this interface.
public sealed class SpatialXRAndroidGradle : IPostGenerateGradleAndroidProject
{
    public int callbackOrder { get { return 1000; } }

    public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
    {
        var gradleRoot = Path.GetFullPath(Path.Combine(unityLibraryPath, ".."));
        var sdkRoot = FindSdk34();

        var localProperties = Path.Combine(gradleRoot, "local.properties");
        File.WriteAllText(localProperties,
            "sdk.dir=" + sdkRoot.Replace("\\", "\\\\").Replace(":", "\\:") + "\n");

        UpdateModule(Path.Combine(unityLibraryPath, "build.gradle"), true);
        UpdateModule(Path.Combine(gradleRoot, "launcher", "build.gradle"), false);

        var gradleProperties = Path.Combine(gradleRoot, "gradle.properties");
        var properties = File.ReadAllText(gradleProperties);
        if (!properties.Contains("android.useAndroidX="))
            properties += "\nandroid.useAndroidX=true\n";
        if (!properties.Contains("android.enableJetifier="))
            properties += "android.enableJetifier=true\n";
        File.WriteAllText(gradleProperties, properties);
    }

    private static string FindSdk34()
    {
        var candidates = new[] {
            Environment.GetEnvironmentVariable("SPATIALAI_ANDROID_SDK_ROOT"),
            Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
            Environment.GetEnvironmentVariable("ANDROID_HOME"),
            @"D:\AndroidSdk\sdk"
        };
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrEmpty(candidate)) continue;
            if (File.Exists(Path.Combine(candidate, "platforms", "android-34", "android.jar")) &&
                File.Exists(Path.Combine(candidate, "build-tools", "30.0.3", "aapt2.exe")))
                return Path.GetFullPath(candidate);
        }
        throw new InvalidOperationException(
            "Spatial XR Unity 2022 sample needs Android SDK platform 34 and Build Tools 30.0.3. " +
            "Set SPATIALAI_ANDROID_SDK_ROOT to the installed SDK directory.");
    }

    private static void UpdateModule(string path, bool addCardboardDependencies)
    {
        var source = File.ReadAllText(path);
        source = Regex.Replace(source, @"compileSdkVersion\s+\d+", "compileSdkVersion 34");
        source = Regex.Replace(source, "buildToolsVersion '[^']+'", "buildToolsVersion '30.0.3'");
        if (addCardboardDependencies && !source.Contains("androidx.appcompat:appcompat"))
        {
            const string marker = "dependencies {";
            if (!source.Contains(marker))
                throw new InvalidOperationException("Unity library Gradle dependencies block was not found.");
            source = source.Replace(marker, marker + "\n" +
                "    implementation 'androidx.appcompat:appcompat:1.6.1'\n" +
                "    implementation 'com.google.android.gms:play-services-vision:20.1.3'\n" +
                "    implementation 'com.google.android.material:material:1.12.0'\n" +
                "    implementation 'com.google.protobuf:protobuf-javalite:3.19.4'\n");
        }
        File.WriteAllText(path, source);
    }
}

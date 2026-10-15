using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

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
        ConfigureRhinoXLauncher(unityLibraryPath);

        var gradleProperties = Path.Combine(gradleRoot, "gradle.properties");
        var properties = File.ReadAllText(gradleProperties);
        if (!properties.Contains("android.useAndroidX="))
            properties += "\nandroid.useAndroidX=true\n";
        if (!properties.Contains("android.enableJetifier="))
            properties += "android.enableJetifier=true\n";
        // Unity's parallel Gradle setting has intermittently locked classes2.dex
        // during mergeDexRelease on the Windows development machine.
        if (properties.Contains("org.gradle.parallel="))
            properties = Regex.Replace(properties, @"(?m)^org\.gradle\.parallel=.*$", "org.gradle.parallel=false");
        else
            properties += "\norg.gradle.parallel=false\n";
        if (properties.Contains("org.gradle.workers.max="))
            properties = Regex.Replace(properties, @"(?m)^org\.gradle\.workers\.max=.*$", "org.gradle.workers.max=1");
        else
            properties += "org.gradle.workers.max=1\n";
        File.WriteAllText(gradleProperties, properties);
    }

    private static void ConfigureRhinoXLauncher(string unityLibraryPath)
    {
        const string androidNamespace = "http://schemas.android.com/apk/res/android";
        var manifestPath = Path.Combine(unityLibraryPath, "src", "main", "AndroidManifest.xml");
        var manifest = new XmlDocument();
        manifest.Load(manifestPath);
        var namespaces = new XmlNamespaceManager(manifest.NameTable);
        namespaces.AddNamespace("android", androidNamespace);
        var activity = manifest.SelectSingleNode(
            "/manifest/application/activity[meta-data[@android:name='unityplayer.UnityActivity' and @android:value='true']]",
            namespaces);
        if (activity == null) throw new InvalidOperationException("Unity main Activity missing from generated Android Manifest.");

        var launcherFilter = activity.SelectSingleNode(
            "intent-filter[action[@android:name='android.intent.action.MAIN'] and category[@android:name='android.intent.category.LAUNCHER']]",
            namespaces);
        if (launcherFilter == null) throw new InvalidOperationException("Unity launcher intent-filter missing from generated Android Manifest.");
        foreach (var name in new[] { "com.qti.intent.category.SNAPDRAGON_VR", "com.ximmerse.category.WONDERLAND_AR" })
        {
            if (launcherFilter.SelectSingleNode("category[@android:name='" + name + "']", namespaces) != null) continue;
            var category = manifest.CreateElement("category");
            SetAndroidAttribute(category, "name", name);
            launcherFilter.AppendChild(category);
        }

        manifest.Save(manifestPath);
    }

    private static void SetAndroidAttribute(XmlElement element, string name, string value)
    {
        var attribute = element.OwnerDocument.CreateAttribute("android", name, "http://schemas.android.com/apk/res/android");
        attribute.Value = value;
        element.SetAttributeNode(attribute);
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
        source = Regex.Replace(source, @"minSdkVersion\s+\d+", "minSdkVersion 28");
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
        if (addCardboardDependencies && !source.Contains("com.google.mediapipe:tasks-vision"))
        {
            const string marker = "dependencies {";
            if (!source.Contains(marker))
                throw new InvalidOperationException("Unity library Gradle dependencies block was not found.");
            source = source.Replace(marker, marker + "\n" +
                "    implementation 'com.google.mediapipe:tasks-vision:1.0.0'\n");
        }
        File.WriteAllText(path, source);
    }
}

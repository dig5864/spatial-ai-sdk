using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;

namespace SpatialAI.Editor
{
    /// <summary>Supply the dependencies of the local hand AAR to every Android consumer.</summary>
    public sealed class SpatialHandGradleDependencies : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder { get { return 990; } }

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            // Unity 2022's AGP 7.1.2 dexer crashes on UVCAndroid 1.0.13.
            // Use the R8 project's supported settings-level override for this toolchain.
            var root = Path.GetFullPath(Path.Combine(unityLibraryPath, ".."));
            ConfigureAndroidProject(root, unityLibraryPath);
            var rootBuild = Path.Combine(root, "build.gradle");
            if (File.Exists(rootBuild) && File.ReadAllText(rootBuild).Contains("7.1.2"))
            {
                var settingsPath = Path.Combine(root, "settings.gradle");
                var settings = File.ReadAllText(settingsPath);
                // Unity may reuse an already exported Gradle project between builds.
                settings = settings.Replace("\n    buildscript {\n        repositories { google(); mavenCentral(); maven { url 'https://storage.googleapis.com/r8-releases/raw' } }\n" +
                    "        dependencies { classpath 'com.android.tools:r8:8.3.55' }\n    }\n", "");
                const string dexer = "com.android.tools:r8:8.3.37";
                if (!settings.Contains(dexer))
                {
                    const string pluginMarker = "pluginManagement {";
                    if (!settings.Contains(pluginMarker)) throw new InvalidOperationException("Missing Gradle pluginManagement block.");
                    var localDexer = Environment.GetEnvironmentVariable("SPATIALAI_R8_JAR");
                    var dependencyLine = !string.IsNullOrEmpty(localDexer) && File.Exists(localDexer)
                        ? "classpath files('" + Path.GetFullPath(localDexer).Replace("\\", "/").Replace("'", "\\'") + "') // " + dexer
                        : "classpath '" + dexer + "'";
                    settings = settings.Replace(pluginMarker, pluginMarker +
                        "\n    buildscript {\n        repositories { google(); mavenCentral() }\n" +
                        "        dependencies { " + dependencyLine + "\n        }\n    }\n");
                    File.WriteAllText(settingsPath, settings);
                }
                else File.WriteAllText(settingsPath, settings);
            }
            var gradle = Path.Combine(unityLibraryPath, "build.gradle");
            var source = File.ReadAllText(gradle);
            const string dependency = "implementation 'com.google.mediapipe:tasks-vision:1.0.0'";
            const string uvcDependency = "implementation 'com.herohan:UVCAndroid:1.0.13'";
            const string marker = "dependencies {";
            if (!source.Contains(marker))
                throw new InvalidOperationException("Unity Android library has no Gradle dependencies block.");
            if (!source.Contains(dependency)) source = source.Replace(marker, marker + "\n    " + dependency + "\n");
            if (!source.Contains(uvcDependency)) source = source.Replace(marker, marker + "\n    " + uvcDependency + "\n");
            File.WriteAllText(gradle, source);
        }

        private static void ConfigureAndroidProject(string root, string library)
        {
            var sdk = Environment.GetEnvironmentVariable("SPATIALAI_ANDROID_SDK_ROOT");
            if (string.IsNullOrWhiteSpace(sdk)) sdk = AndroidExternalToolsSettings.sdkRootPath;
            if (string.IsNullOrWhiteSpace(sdk) || !File.Exists(Path.Combine(sdk, "platforms/android-34/android.jar")))
                throw new InvalidOperationException("Spatial Android: install SDK Platform 34 and configure Unity External Tools > Android SDK, or SPATIALAI_ANDROID_SDK_ROOT.");
            if (!Directory.Exists(Path.Combine(sdk, "build-tools/30.0.3")))
                throw new InvalidOperationException("Spatial Android Unity 2022 baseline requires Android Build Tools 30.0.3 in the configured SDK.");
            File.WriteAllText(Path.Combine(root, "local.properties"), "sdk.dir=" + Path.GetFullPath(sdk).Replace("\\", "\\\\").Replace(":", "\\:") + "\n");
            foreach (var module in new[] { Path.Combine(library, "build.gradle"), Path.Combine(root, "launcher/build.gradle") })
            {
                if (!File.Exists(module)) continue;
                var source = File.ReadAllText(module);
                source = Regex.Replace(source, @"compileSdkVersion\s+\d+", "compileSdkVersion 34");
                source = Regex.Replace(source, @"minSdkVersion\s+\d+", "minSdkVersion 28");
                source = Regex.Replace(source, @"targetSdkVersion\s+\d+", "targetSdkVersion 34");
                source = Regex.Replace(source, "buildToolsVersion '[^']+'", "buildToolsVersion '30.0.3'");
                if (module == Path.Combine(library, "build.gradle") && UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.google.xr.cardboard") != null)
                    foreach (var dependency in new[] { "androidx.appcompat:appcompat:1.6.1", "com.google.android.gms:play-services-vision:20.1.3", "com.google.android.material:material:1.12.0", "com.google.protobuf:protobuf-javalite:3.19.4" })
                        if (!source.Contains(dependency)) source = source.Replace("dependencies {", "dependencies {\n    implementation '" + dependency + "'\n");
                File.WriteAllText(module, source);
            }
            var propertiesPath = Path.Combine(root, "gradle.properties");
            var properties = File.Exists(propertiesPath) ? File.ReadAllText(propertiesPath) : "";
            foreach (var entry in new[] { "android.useAndroidX=true", "android.enableJetifier=true", "org.gradle.parallel=false", "org.gradle.workers.max=1" })
            {
                var key = entry.Substring(0, entry.IndexOf('='));
                var pattern = @"(?m)^" + Regex.Escape(key) + @"=.*$";
                properties = Regex.IsMatch(properties, pattern) ? Regex.Replace(properties, pattern, entry) : properties + "\n" + entry + "\n";
            }
            File.WriteAllText(propertiesPath, properties);
        }
    }
}

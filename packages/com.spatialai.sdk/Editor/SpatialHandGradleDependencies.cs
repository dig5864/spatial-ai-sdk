using System;
using System.IO;
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
    }
}

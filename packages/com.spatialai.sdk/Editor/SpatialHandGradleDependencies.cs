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
            var gradle = Path.Combine(unityLibraryPath, "build.gradle");
            var source = File.ReadAllText(gradle);
            const string dependency = "implementation 'com.google.mediapipe:tasks-vision:1.0.0'";
            if (source.Contains(dependency)) return;
            const string marker = "dependencies {";
            if (!source.Contains(marker))
                throw new InvalidOperationException("Unity Android library has no Gradle dependencies block.");
            File.WriteAllText(gradle, source.Replace(marker, marker + "\n    " + dependency + "\n"));
        }
    }
}

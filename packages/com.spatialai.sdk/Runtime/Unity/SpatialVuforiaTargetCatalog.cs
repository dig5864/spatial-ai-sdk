using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SpatialAI.Unity
{
    /// <summary>The default Vuforia target images and their physical print dimensions shipped with the SDK.</summary>
    public static class SpatialVuforiaTargetCatalog
    {
        private const string ResourcePath = "SpatialAI/VuforiaTargets/";

        [Serializable] private sealed class Manifest { public int version; public Entry[] targets; }
        [Serializable] private sealed class Entry
        {
            public string id, name, image;
            public int defaultWidthMm;
            public int[] widthsMm;
        }

        public static IReadOnlyList<Target> LoadBuiltIn()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath + "targets");
            if (asset == null) throw new InvalidOperationException("The SDK Vuforia target catalog is missing.");
            var manifest = JsonUtility.FromJson<Manifest>(asset.text);
            if (manifest == null || manifest.version != 1 || manifest.targets == null || manifest.targets.Length == 0)
                throw new InvalidDataException("Unsupported or empty SDK Vuforia target catalog.");

            var targets = new List<Target>(manifest.targets.Length);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var images = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in manifest.targets)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || !ids.Add(entry.id) ||
                    string.IsNullOrWhiteSpace(entry.name) || string.IsNullOrEmpty(entry.image) ||
                    !entry.image.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    entry.image != Path.GetFileName(entry.image) || !images.Add(entry.image) ||
                    entry.widthsMm == null || Array.Exists(entry.widthsMm, width => width <= 0) ||
                    Array.IndexOf(entry.widthsMm, entry.defaultWidthMm) < 0)
                    throw new InvalidDataException("Invalid or duplicate SDK Vuforia target catalog entry.");

                var image = Resources.Load<TextAsset>(ResourcePath + Path.GetFileNameWithoutExtension(entry.image));
                if (image == null) throw new InvalidDataException("Missing SDK Vuforia target image: " + entry.image);
                targets.Add(new Target(entry.id, entry.name, entry.image, entry.defaultWidthMm, entry.widthsMm, image));
            }
            return targets.AsReadOnly();
        }

        public sealed class Target
        {
            private readonly TextAsset image;
            public string Id { get; }
            public string Name { get; }
            public string ImageFileName { get; }
            public int DefaultWidthMm { get; }
            public IReadOnlyList<int> SupportedWidthsMm { get; }
            public float AspectRatio { get; }

            internal Target(string id, string name, string filename, int defaultWidthMm, int[] widthsMm, TextAsset image)
            {
                Id = id;
                Name = name;
                ImageFileName = filename;
                DefaultWidthMm = defaultWidthMm;
                SupportedWidthsMm = Array.AsReadOnly(widthsMm);
                this.image = image;

                var texture = new Texture2D(2, 2);
                try
                {
                    if (!ImageConversion.LoadImage(texture, image.bytes, markNonReadable: true))
                        throw new InvalidDataException("Invalid SDK Vuforia target image: " + filename);
                    AspectRatio = (float)texture.width / texture.height;
                }
                finally
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
                    else UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            /// <summary>Resolves a print-width preset. Pass zero to use the SDK default.</summary>
            public int ResolveWidthMm(int widthMm = 0)
            {
                if (widthMm == 0) return DefaultWidthMm;
                foreach (int supported in SupportedWidthsMm)
                    if (supported == widthMm) return widthMm;
                throw new ArgumentOutOfRangeException(nameof(widthMm), "Unsupported print width for " + Id);
            }

            /// <summary>Copies the SDK image to a normal file for the native Vuforia bridge.</summary>
            public string MaterializeImage()
            {
                var directory = Path.Combine(Application.persistentDataPath, "spatial-vuforia-targets");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, ImageFileName);
                File.WriteAllBytes(path, image.bytes);
                return path;
            }
        }
    }
}

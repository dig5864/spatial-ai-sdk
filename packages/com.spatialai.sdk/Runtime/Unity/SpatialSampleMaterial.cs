using UnityEngine;

namespace SpatialAI.Unity
{
    /// <summary>Unlit sample material shared by Built-in and URP consumers.</summary>
    public static class SpatialSampleMaterial
    {
        public static Material Create(Color color)
        {
            // A Resources reference also keeps this runtime-created shader in player builds.
            return new Material(Resources.Load<Shader>("SpatialAI/SpatialUnlit")) { color = color };
        }
    }
}

#if SPATIALAI_HAS_CARDBOARD
using System;
using UnityEngine;
namespace SpatialAI.Cardboard
{
    /// <summary>Fallback prediction used only when a valid display timeline is unavailable.
    /// Call on Unity's main thread. The application owns persistence.</summary>
    public static class SpatialPosePrediction
    {
        public const int DefaultFallbackMilliseconds = 50;
        public static int FallbackMilliseconds { get; private set; } = DefaultFallbackMilliseconds;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { FallbackMilliseconds = DefaultFallbackMilliseconds; }
        public static void SetFallbackMilliseconds(int milliseconds)
        {
            if (milliseconds < 0 || milliseconds > 100)
                throw new ArgumentOutOfRangeException(nameof(milliseconds), "Use 0 to 100 ms.");
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var timing = new AndroidJavaClass("com.spatialai.cardboard.SpatialFrameTiming"))
                timing.CallStatic("setFallbackPredictionMs", milliseconds);
#endif
            FallbackMilliseconds = milliseconds;
            Debug.Log("SpatialAI fallback pose prediction requested: " + milliseconds + " ms");
        }
    }
}
#endif

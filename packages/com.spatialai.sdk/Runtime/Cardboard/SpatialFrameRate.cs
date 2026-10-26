#if SPATIALAI_HAS_CARDBOARD
using System;
using UnityEngine;

namespace SpatialAI.Cardboard
{
    public enum SpatialFrameRateMode { FollowDisplay = 0, Locked60 = 1 }

    [Serializable]
    public struct SpatialFrameRateConfiguration
    {
        public SpatialFrameRateMode mode;
        [Tooltip("Software limiter headroom relative to the actual display rate. Minimum 2; this is not the rendered FPS.")]
        [Range(2f, 4f)] public float softwareHeadroomMultiplier;

        public SpatialFrameRateConfiguration(SpatialFrameRateMode mode, float softwareHeadroomMultiplier = 2f)
        {
            this.mode = mode;
            this.softwareHeadroomMultiplier = softwareHeadroomMultiplier;
        }
        public static SpatialFrameRateConfiguration Default => new SpatialFrameRateConfiguration(SpatialFrameRateMode.FollowDisplay);
    }

    public struct SpatialFrameRateStatus
    {
        public bool IsActive { get; internal set; }
        public SpatialFrameRateMode Mode { get; internal set; }
        public float DisplayRefreshHz { get; internal set; }
        public float TargetFrameRate { get; internal set; }
        public int SoftwareFrameRateCap { get; internal set; }
        public float SoftwareHeadroomMultiplier { get; internal set; }
        public int CadenceDivisor { get; internal set; }
        public bool FractionalCadence { get; internal set; }
        public bool DisplayModeRequestApplied { get; internal set; }
        public int Generation { get; internal set; }
        public int ConfigurationVersion { get; internal set; }
    }

    /// <summary>Android Cardboard frame-rate policy. Call Configure on Unity's main thread.
    /// Configuration persists across scene changes; the SDK does not save application preferences.
    /// TargetFrameRate is a policy target. Actual presentation must be measured separately.</summary>
    public static class SpatialFrameRate
    {
        private static SpatialFrameRateConfiguration configuration = SpatialFrameRateConfiguration.Default;
        private static bool explicitlyConfigured;
        public static SpatialFrameRateConfiguration Configuration => configuration;
        public static SpatialFrameRateStatus Status { get; private set; }
        public static int ConfigurationVersion { get; private set; } = 1;
        public static event Action<SpatialFrameRateConfiguration> ConfigurationChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            configuration = SpatialFrameRateConfiguration.Default;
            explicitlyConfigured = false;
            Status = default;
            ConfigurationVersion = 1;
            ConfigurationChanged = null;
        }

        public static void Configure(SpatialFrameRateMode mode, float softwareHeadroomMultiplier = 2f)
        {
            Configure(new SpatialFrameRateConfiguration(mode, softwareHeadroomMultiplier));
        }

        public static void Configure(SpatialFrameRateConfiguration value)
        {
            Validate(value);
            explicitlyConfigured = true;
            Apply(value);
            EnsureController();
        }

        internal static void Initialize(SpatialFrameRateConfiguration value)
        {
            if (!explicitlyConfigured)
            {
                Validate(value);
                Apply(value);
            }
            EnsureController();
        }

        private static void EnsureController()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Application.isPlaying || SpatialAndroidFramePacing.HasController) return;
            var controller = new GameObject("SpatialAI Frame Rate");
            UnityEngine.Object.DontDestroyOnLoad(controller);
            controller.AddComponent<SpatialAndroidFramePacing>();
#endif
        }

        private static void Validate(SpatialFrameRateConfiguration value)
        {
            if (!Enum.IsDefined(typeof(SpatialFrameRateMode), value.mode))
                throw new ArgumentOutOfRangeException(nameof(value.mode));
            if (float.IsNaN(value.softwareHeadroomMultiplier) || float.IsInfinity(value.softwareHeadroomMultiplier) ||
                value.softwareHeadroomMultiplier < 2f || value.softwareHeadroomMultiplier > 4f)
                throw new ArgumentOutOfRangeException(nameof(value.softwareHeadroomMultiplier), "Use headroom from 2 to 4.");
        }

        private static void Apply(SpatialFrameRateConfiguration value)
        {
            if (configuration.mode == value.mode && configuration.softwareHeadroomMultiplier == value.softwareHeadroomMultiplier) return;
            configuration = value;
            ConfigurationVersion = ConfigurationVersion == int.MaxValue ? 1 : ConfigurationVersion + 1;
            Debug.Log("SpatialAI FRAME-RATE requested mode=" + value.mode + " headroom=" + value.softwareHeadroomMultiplier +
                " version=" + ConfigurationVersion);
            ConfigurationChanged?.Invoke(value);
        }

        internal static void PublishStatus(SpatialFrameRateStatus value) { Status = value; }
        internal static void MarkInactive()
        {
            var value = Status; value.IsActive = false; Status = value;
        }
    }
}
#endif

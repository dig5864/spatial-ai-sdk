#if SPATIALAI_HAS_CARDBOARD
using UnityEngine;
using UnityEngine.Rendering;

namespace SpatialAI.Cardboard
{
    [DefaultExecutionOrder(10000)]
    public sealed class SpatialAndroidFramePacing : MonoBehaviour
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        [System.Serializable]
        private sealed class AndroidStatus
        {
            public bool active;
            public int policyMode, configurationVersion, generation, softwareFrameRateCap, divisor;
            public float modeHz, targetFps, softwareHeadroomMultiplier;
            public bool fractionalCadence, modeRequestApplied;
        }
        private static SpatialAndroidFramePacing owner;
        internal static bool HasController => owner != null;
        private AndroidJavaClass timing;
        private int previousFps, previousInterval;
        private float nextPoll;
        private string previousStatus;
        private bool paused;

        private void OnEnable()
        {
            if (owner != null && owner != this)
            {
                Debug.LogWarning("SpatialAI FRAME-RATE already has an active controller.");
                enabled = false; return;
            }
            owner = this;
            previousFps = Application.targetFrameRate;
            previousInterval = OnDemandRendering.renderFrameInterval;
            SpatialFrameRate.ConfigurationChanged += ApplyConfiguration;
            try
            {
                timing = new AndroidJavaClass("com.spatialai.cardboard.SpatialFrameTiming");
                var config = SpatialFrameRate.Configuration;
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    timing.CallStatic("start", activity, (int)config.mode, config.softwareHeadroomMultiplier,
                        SpatialFrameRate.ConfigurationVersion);
                nextPoll = 0;
            }
            catch (System.Exception error)
            {
                if (timing != null) { timing.Dispose(); timing = null; }
                SpatialFrameRate.MarkInactive();
                Debug.LogError("SpatialAI FRAME-RATE unavailable: " + error);
            }
        }

        private void ApplyConfiguration(SpatialFrameRateConfiguration config)
        {
            if (timing == null) return;
            try
            {
                timing.CallStatic("configure", (int)config.mode, config.softwareHeadroomMultiplier,
                    SpatialFrameRate.ConfigurationVersion);
            }
            catch (System.Exception error)
            {
                SpatialFrameRate.MarkInactive();
                Debug.LogError("SpatialAI FRAME-RATE configuration failed: " + error);
            }
            previousStatus = null; nextPoll = 0;
        }

        private void LateUpdate()
        {
            if (timing == null || paused || Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + .5f;
            var json = timing.CallStatic<string>("getStatusJson");
            if (string.IsNullOrEmpty(json)) return;
            if (json != previousStatus)
            {
                var state = JsonUtility.FromJson<AndroidStatus>(json);
                if (state == null || !state.active || state.configurationVersion != SpatialFrameRate.ConfigurationVersion ||
                    state.policyMode != (int)SpatialFrameRate.Configuration.mode || state.modeHz < 1 ||
                    state.softwareFrameRateCap < 1 || state.softwareFrameRateCap > 4000) return;
                SpatialFrameRate.PublishStatus(new SpatialFrameRateStatus {
                    IsActive = true, Mode = (SpatialFrameRateMode)state.policyMode,
                    DisplayRefreshHz = state.modeHz, TargetFrameRate = state.targetFps,
                    SoftwareFrameRateCap = state.softwareFrameRateCap,
                    SoftwareHeadroomMultiplier = state.softwareHeadroomMultiplier,
                    CadenceDivisor = state.divisor, FractionalCadence = state.fractionalCadence,
                    DisplayModeRequestApplied = state.modeRequestApplied,
                    Generation = state.generation, ConfigurationVersion = state.configurationVersion
                });
                previousStatus = json;
                Debug.Log("SpatialAI FRAME-RATE applied " + json + " actualPresentationMeasured=false");
            }
            var status = SpatialFrameRate.Status;
            if (status.IsActive && status.ConfigurationVersion == SpatialFrameRate.ConfigurationVersion)
            {
                if (Application.targetFrameRate != status.SoftwareFrameRateCap)
                    Application.targetFrameRate = status.SoftwareFrameRateCap;
                if (OnDemandRendering.renderFrameInterval != 1)
                    OnDemandRendering.renderFrameInterval = 1;
            }
        }

        private void OnApplicationPause(bool value)
        {
            paused = value;
            if (timing != null)
            {
                if (!value) ApplyConfiguration(SpatialFrameRate.Configuration);
                timing.CallStatic("setPaused", value);
            }
            if (value) SpatialFrameRate.MarkInactive();
            previousStatus = null; nextPoll = 0;
        }

        private void OnDisable()
        {
            SpatialFrameRate.ConfigurationChanged -= ApplyConfiguration;
            if (owner != this) return;
            if (timing != null) { timing.CallStatic("stop"); timing.Dispose(); timing = null; }
            Application.targetFrameRate = previousFps;
            OnDemandRendering.renderFrameInterval = previousInterval;
            SpatialFrameRate.MarkInactive(); owner = null;
        }
#endif
    }
}
#endif

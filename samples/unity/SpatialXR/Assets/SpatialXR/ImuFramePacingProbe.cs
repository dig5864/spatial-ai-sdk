#if SPATIALAI_HAS_CARDBOARD
using System;
using System.Globalization;
using UnityEngine;

namespace SpatialAI.Samples
{
    // Wall-clock samples are collected without per-frame logging or allocations.
    // These are player-loop timings, not physical presentation or sensor timestamps.
    internal sealed class ImuFramePacingProbe
    {
        private const string Variant = "sdk-frame-rate-v1";

        private readonly double[] intervals = new double[2048];
        private readonly double[] sorted = new double[2048];
        private double windowStart, previousUpdate, latestUpdate, previousBeforeRender;
        private double beforeRenderMaxMs, updateToRenderMaxMs;
        private double reportedRefreshHzAtConfig, previousReportMs;
        private int samples, overflow, beforeRenderCount, lastRenderFrame = -1, window, gcStart;
        private bool headerPending = true;
        private string resetReason = "start";
        private static double Now => (double)System.Diagnostics.Stopwatch.GetTimestamp() /
            System.Diagnostics.Stopwatch.Frequency;

        public void Reset(string reason)
        {
            windowStart = Now;
            previousUpdate = latestUpdate = previousBeforeRender = 0;
            beforeRenderMaxMs = updateToRenderMaxMs = 0;
            previousReportMs = 0;
            samples = overflow = beforeRenderCount = 0;
            lastRenderFrame = -1;
            window = 0;
            gcStart = GC.CollectionCount(0);
            resetReason = reason;
            headerPending = true;
        }

        public void BeforeRender()
        {
            if (lastRenderFrame == Time.frameCount) return;
            var now = Now;
            lastRenderFrame = Time.frameCount;
            beforeRenderCount++;
            if (previousBeforeRender > 0)
                beforeRenderMaxMs = Math.Max(beforeRenderMaxMs, (now - previousBeforeRender) * 1000);
            if (latestUpdate > 0)
                updateToRenderMaxMs = Math.Max(updateToRenderMaxMs, (now - latestUpdate) * 1000);
            previousBeforeRender = now;
        }

        public void Update(bool xrRunning, bool debugUiVisible)
        {
            var now = Now;
            if (headerPending)
            {
                reportedRefreshHzAtConfig = Screen.currentResolution.refreshRateRatio.value;
                Debug.Log("SpatialAI IMU-PACING CONFIG round=1 variant=" + Variant +
                    " build=" + Application.buildGUID + " unity=" + Application.unityVersion +
                    " reason=" + resetReason + " softwareCap=" + Application.targetFrameRate +
                    " policyMode=" + SpatialAI.Cardboard.SpatialFrameRate.Configuration.mode +
                    " policyTargetFps=" + SpatialAI.Cardboard.SpatialFrameRate.Status.TargetFrameRate +
                    " reportedRefreshHzAtConfig=" + reportedRefreshHzAtConfig.ToString("F3", CultureInfo.InvariantCulture) +
                    " vSyncCount=" + QualitySettings.vSyncCount +
                    " renderInterval=" + UnityEngine.Rendering.OnDemandRendering.renderFrameInterval +
                    " graphics=" + SystemInfo.graphicsDeviceType +
                    " debugUiVisible=" + debugUiVisible + " uiRevision=gear-toggle-v1" +
                    " gearRevision=" + SpatialAI.Cardboard.SpatialScreenCalibration.NativeGearLayoutRevision +
                    " clock=Stopwatch windowSeconds=5 presentationMeasured=false");
                headerPending = false;
                // Exclude the header's startup I/O from the first interval.
                windowStart = now = Now;
            }
            if (previousUpdate > 0)
            {
                if (samples < intervals.Length) intervals[samples++] = (now - previousUpdate) * 1000;
                else overflow++;
            }
            latestUpdate = previousUpdate = now;
            var seconds = now - windowStart;
            if (seconds < 5 || samples == 0) return;

            var reportStart = Now;
            Array.Copy(intervals, sorted, samples);
            Array.Sort(sorted, 0, samples);
            var over25 = 0; var over33 = 0; var over50 = 0;
            double sum = 0;
            for (var i = 0; i < samples; i++)
            {
                var value = intervals[i]; sum += value;
                if (value > 25) over25++;
                if (value > 33.333) over33++;
                if (value > 50) over50++;
            }
            var collections = GC.CollectionCount(0);
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "SpatialAI IMU-PACING WINDOW round=1 variant={0} index={1} warmup={2} t={3:F3} seconds={4:F3} n={5} overflow={6}" +
                " updateFps={7:F2} meanMs={8:F3} p50Ms={9:F3} p95Ms={10:F3} p99Ms={11:F3} maxMs={12:F3}" +
                " over25={13} over33_333={14} over50={15} beforeRenderN={16} beforeRenderMaxMs={17:F3}" +
                " updateToBeforeRenderMaxMs={18:F3} gc0Delta={19} previousReportMs={20:F3} xrRunning={21} softwareCap={22} reportedRefreshHzAtConfig={23:F3}" +
                " debugUiVisible={24} uiRevision=gear-toggle-v1 gearRevision={25} policyMode={26} policyTargetFps={27:F2} configuredHeadroom={28:F2}",
                Variant, ++window, window == 1, Time.realtimeSinceStartupAsDouble, seconds, samples, overflow,
                samples * 1000 / sum, sum / samples, Percentile(.50), Percentile(.95), Percentile(.99), sorted[samples - 1],
                over25, over33, over50, beforeRenderCount, beforeRenderMaxMs, updateToRenderMaxMs,
                collections - gcStart, previousReportMs, xrRunning,
                Application.targetFrameRate, reportedRefreshHzAtConfig, debugUiVisible,
                SpatialAI.Cardboard.SpatialScreenCalibration.NativeGearLayoutRevision,
                SpatialAI.Cardboard.SpatialFrameRate.Configuration.mode,
                SpatialAI.Cardboard.SpatialFrameRate.Status.TargetFrameRate,
                SpatialAI.Cardboard.SpatialFrameRate.Configuration.softwareHeadroomMultiplier));
            previousReportMs = (Now - reportStart) * 1000;
            gcStart = collections;
            windowStart = now;
            samples = overflow = beforeRenderCount = 0;
            beforeRenderMaxMs = updateToRenderMaxMs = 0;
        }

        private double Percentile(double fraction)
        {
            return sorted[Math.Max(0, (int)Math.Ceiling(samples * fraction) - 1)];
        }
    }
}
#endif

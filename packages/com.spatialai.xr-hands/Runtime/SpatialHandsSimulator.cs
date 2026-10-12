using System.Collections.Generic;
using SpatialAI.Core;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace SpatialAI.XRHands
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class SpatialHandsSimulator : MonoBehaviour
    {
        public SimulatedHandState left = new SimulatedHandState { position = new Vector3(-0.13f, -0.08f, 0.55f) };
        public SimulatedHandState right = new SimulatedHandState { position = new Vector3(0.13f, -0.08f, 0.55f) };
        public bool animate;
        public SpatialHandSubsystem Subsystem { get; private set; }
        public SimulatedHandsFrameSource Source { get; private set; }
        private readonly MonotonicClock clock = new MonotonicClock();
        private readonly List<UnityEngine.XR.XRDisplaySubsystem> displays = new List<UnityEngine.XR.XRDisplaySubsystem>();
        private int dynamicFrame = -1, renderFrame = -1;
        private double startTime;
        private Vector3 leftBase, rightBase, leftAngles, rightAngles;

        private void OnEnable() { if (Application.isPlaying) StartSimulation(); }
        private void OnDisable() { StopSimulation(); }
        private void OnDestroy() { StopSimulation(); }
        public bool StartSimulation()
        {
            if (Subsystem != null) return true;
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var existing in subsystems)
                if (existing is SpatialHandSubsystem)
                {
                    Debug.LogWarning("A Spatial hands simulator already owns the subsystem. The duplicate was not started.", this);
                    return false;
                }
            Source = new SimulatedHandsFrameSource();
            Subsystem = SpatialHandSubsystem.Create(Source);
            leftBase = left.position; rightBase = right.position;
            leftAngles = left.eulerAngles; rightAngles = right.eulerAngles;
            startTime = Time.realtimeSinceStartupAsDouble;
            dynamicFrame = renderFrame = -1;
            Source.Publish(left, right, clock.NowNs);
            Subsystem.Start();
            Application.onBeforeRender += BeforeRender;
            return true;
        }
        public void StopSimulation()
        {
            Application.onBeforeRender -= BeforeRender;
            if (Subsystem == null) return;
            // Notify ordinary XR Hands consumers before stopping, and overwrite
            // every joint so they cannot observe a stale valid pose after loss.
            Subsystem.Stop();
            Subsystem.Destroy();
            Subsystem = null; Source = null;
            dynamicFrame = renderFrame = -1;
        }
        public void PublishAndUpdate(XRHandSubsystem.UpdateType updateType)
        {
            if (Subsystem == null || !Subsystem.running) return;
            if (updateType == XRHandSubsystem.UpdateType.Dynamic) Source.Publish(left, right, clock.NowNs);
            // BeforeRender uses the same snapshot as Dynamic; no new inference
            // or duplicate source sequence is manufactured for the render phase.
            Subsystem.TryUpdateHands(updateType);
        }
        private void Update()
        {
            if (Subsystem == null || dynamicFrame == Time.frameCount) return;
            dynamicFrame = Time.frameCount;
            if (animate) Animate((float)(Time.realtimeSinceStartupAsDouble - startTime));
            PublishAndUpdate(XRHandSubsystem.UpdateType.Dynamic);
        }
        private void LateUpdate()
        {
            // A desktop simulation scene has no XR display, and the Editor may
            // not emit onBeforeRender. Give consumers a render-phase update there.
            // With an active XR display, retain the real onBeforeRender timing.
            displays.Clear(); SubsystemManager.GetSubsystems(displays);
            foreach (var display in displays) if (display.running) return;
            BeforeRender();
        }
        [BeforeRenderOrder(-100)]
        private void BeforeRender()
        {
            if (Subsystem == null || renderFrame == Time.frameCount) return;
            renderFrame = Time.frameCount;
            PublishAndUpdate(XRHandSubsystem.UpdateType.BeforeRender);
        }
        private void Animate(float time)
        {
            left.position = leftBase + new Vector3(Mathf.Sin(time) * 0.045f, Mathf.Cos(time * 0.8f) * 0.02f, 0);
            right.position = rightBase + new Vector3(0, Mathf.Sin(time * 1.1f) * 0.04f, Mathf.Cos(time) * 0.025f);
            left.eulerAngles = leftAngles + new Vector3(0, 0, Mathf.Sin(time) * 20);
            right.eulerAngles = rightAngles + new Vector3(Mathf.Sin(time * 0.7f) * 15, Mathf.Cos(time) * 15, 0);
        }
    }
}

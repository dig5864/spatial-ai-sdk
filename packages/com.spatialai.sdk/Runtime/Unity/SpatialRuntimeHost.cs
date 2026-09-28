using UnityEngine;
using SpatialAI.Core;

namespace SpatialAI.Unity
{
    public enum BackendSelection { Simulator, Cardboard, RhinoXPro }

    [DisallowMultipleComponent]
    public sealed class SpatialRuntimeHost : MonoBehaviour
    {
        public BackendSelection backend = BackendSelection.Simulator;
        public SpatialSession Session { get; private set; }
        public SimulatorBackend Simulator { get; private set; }

        private void OnEnable()
        {
            IRuntimeBackend source;
            if (backend == BackendSelection.Simulator)
            { Simulator = new SimulatorBackend(); source = Simulator; }
            else source = new PendingHardwareBackend(backend == BackendSelection.Cardboard ? HardwareTarget.Cardboard : HardwareTarget.RhinoXPro);
            Session = new SpatialSession(source);
            var result = Session.Start();
            if (!result.Success) Debug.LogError("Spatial AI SDK: " + result.Message, this);
        }
        // Read after ordinary Update input, before sample rendering in LateUpdate.
        private void Update() { if (Session != null && Session.State == SessionState.Running) Session.Tick(); }
        private void OnApplicationPause(bool paused)
        {
            if (Session == null) return;
            if (paused) Session.Suspend();
            else if (Session.State == SessionState.Suspended) Session.Start();
        }
        private void OnDisable()
        {
            if (Session != null) Session.Dispose();
            Session = null; Simulator = null;
        }
        public static Quaternion ToUnity(Rotation rotation) { return new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W); }
    }
}

namespace SpatialAI.Core
{
    public enum HardwareTarget { Cardboard, RhinoXPro }

    // Fail closed until real adapters are integrated; never silently substitute simulated data.
    public sealed class PendingHardwareBackend : IRuntimeBackend
    {
        private readonly HardwareTarget target;
        public PendingHardwareBackend(HardwareTarget target) { this.target = target; }
        public string Id { get { return target.ToString(); } }
        public bool IsSimulated { get { return false; } }
        public Capability Capabilities { get { return Capability.None; } }
        public Result Start() { return Unsupported(); }
        public void Stop() { }
        public Result ReadFrame(out RuntimeFrame frame) { frame = default(RuntimeFrame); return Unsupported(); }
        public Result Recenter() { return Unsupported(); }
        public void Dispose() { }
        private Result Unsupported() { return new Result(ErrorCode.Unsupported, target + " hardware adapter is not implemented in 0.1.0-preview.1."); }
    }

    // Future native/provider adapters implement these boundaries without leaking vendor types.
    public interface IHeadPoseSource { Result ReadPose(long predictedDisplayTimeNs, out PoseSample pose); }
    public interface IOpticsProvider { Result GetProfile(string deviceId, out OpticsProfile profile); }
    public interface IDisplaySink
    {
        // Must be called on the render thread. Texture ownership and GPU fences require
        // a graphics-API-specific contract before a real implementation can be enabled.
        bool IsAvailable { get; }
        Result Configure(OpticsProfile profile);
        void Shutdown();
    }
    public sealed class OpticsProfile
    {
        public string ProfileId, DeviceModel, CalibrationVersion, MeshAssetHash;
        public int SchemaVersion = 1;
        public bool ValidatedForHardware;
        public int EyeWidth, EyeHeight;
        public float IpdMetres;
        public Result Validate()
        {
            if (SchemaVersion != 1 || string.IsNullOrEmpty(ProfileId) || string.IsNullOrEmpty(DeviceModel) ||
                string.IsNullOrEmpty(CalibrationVersion) || string.IsNullOrEmpty(MeshAssetHash) ||
                EyeWidth <= 0 || EyeHeight <= 0 || float.IsNaN(IpdMetres) || float.IsInfinity(IpdMetres) || IpdMetres <= 0)
                return new Result(ErrorCode.InvalidArgument, "Incomplete optics profile metadata.");
            return ValidatedForHardware ? Result.Ok : new Result(ErrorCode.Unsupported, "Profile has not been calibrated for hardware.");
        }
    }
}

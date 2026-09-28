namespace SpatialAI.Core
{
    public sealed class SimulatorBackend : IRuntimeBackend
    {
        private readonly IClock clock;
        private bool running, disposed, trigger;
        private Rotation head = Rotation.Identity, controller = Rotation.Identity, origin = Rotation.Identity;
        public string Id { get { return "simulator"; } }
        public bool IsSimulated { get { return true; } }
        public Capability Capabilities { get { return Capability.HeadOrientation | Capability.ControllerOrientation | Capability.Trigger; } }
        public SimulatorBackend(IClock clock = null) { this.clock = clock ?? new MonotonicClock(); }
        public Result SetInput(Rotation head, Rotation controller, bool triggerHeld)
        {
            if (disposed) return new Result(ErrorCode.NotRunning, "Simulator disposed.");
            if (!head.IsUnit || !controller.IsUnit) return new Result(ErrorCode.InvalidArgument, "Input must contain unit quaternions.");
            this.head = head; this.controller = controller; trigger = triggerHeld;
            return Result.Ok;
        }
        public Result Start()
        {
            if (disposed) return new Result(ErrorCode.NotRunning, "Simulator disposed.");
            running = true; trigger = false; return Result.Ok;
        }
        public void Stop() { running = false; trigger = false; }
        public Result ReadFrame(out RuntimeFrame frame)
        {
            frame = default(RuntimeFrame);
            if (!running) return new Result(ErrorCode.NotRunning, "Simulator stopped.");
            long now = clock.NowNs;
            frame = new RuntimeFrame(new PoseSample(origin * head, now, true),
                new PoseSample(origin * controller, now, true), trigger);
            return Result.Ok;
        }
        public Result Recenter()
        {
            if (!running) return new Result(ErrorCode.NotRunning, "Simulator stopped.");
            origin = Rotation.FromYawPitch(-head.YawDegrees, 0);
            return Result.Ok;
        }
        public void Dispose() { Stop(); disposed = true; }
    }
}

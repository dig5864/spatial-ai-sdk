using System;

namespace SpatialAI.Core
{
    public sealed class SpatialSession : IDisposable
    {
        private readonly IRuntimeBackend backend;
        private bool triggerWasHeld;
        public SessionState State { get; private set; }
        public RuntimeFrame Frame { get; private set; }
        public Result LastResult { get; private set; }
        public bool TriggerPressed { get; private set; }
        public bool IsSimulated { get { return backend.IsSimulated; } }
        public Capability Capabilities { get { return backend.Capabilities; } }
        public string BackendId { get { return backend.Id; } }

        public SpatialSession(IRuntimeBackend backend)
        {
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            State = SessionState.Stopped;
            LastResult = Result.Ok;
        }
        public Result Start()
        {
            if (State == SessionState.Disposed) return Invalid("Session disposed.");
            if (State == SessionState.Running) return Result.Ok;
            ClearFrame();
            State = SessionState.Starting;
            try { LastResult = backend.Start(); }
            catch (Exception e) { LastResult = new Result(ErrorCode.BackendFailure, e.Message); }
            State = LastResult.Success ? SessionState.Running : SessionState.Error;
            if (!LastResult.Success) SafeStop();
            return LastResult;
        }
        public Result Tick()
        {
            if (State != SessionState.Running) return Invalid("Session is not running.");
            RuntimeFrame frame;
            try { LastResult = backend.ReadFrame(out frame); }
            catch (Exception e) { frame = default(RuntimeFrame); LastResult = new Result(ErrorCode.BackendFailure, e.Message); }
            if (LastResult.Success &&
                ((frame.Head.IsTracked && (!frame.Head.Orientation.IsUnit || frame.Head.TimestampNs < 0)) ||
                 (frame.Controller.IsTracked && (!frame.Controller.Orientation.IsUnit || frame.Controller.TimestampNs < 0))))
                LastResult = new Result(ErrorCode.BackendFailure, "Invalid tracked pose.");
            if (!LastResult.Success)
            {
                SafeStop();
                ClearFrame();
                State = SessionState.Error;
                return LastResult;
            }
            Frame = frame;
            bool held = frame.Controller.IsTracked && frame.TriggerHeld;
            TriggerPressed = held && !triggerWasHeld;
            triggerWasHeld = held;
            return LastResult;
        }
        public Result Recenter()
        {
            if (State != SessionState.Running) return Invalid("Session is not running.");
            try { LastResult = backend.Recenter(); }
            catch (Exception e) { LastResult = new Result(ErrorCode.BackendFailure, e.Message); }
            return LastResult;
        }
        public void Suspend()
        {
            if (State != SessionState.Running) return;
            SafeStop(); ClearFrame(); State = SessionState.Suspended;
        }
        public void Stop()
        {
            if (State == SessionState.Disposed) return;
            SafeStop(); ClearFrame(); State = SessionState.Stopped;
        }
        public void Dispose()
        {
            if (State == SessionState.Disposed) return;
            Stop();
            try { backend.Dispose(); }
            finally { State = SessionState.Disposed; }
        }
        private Result Invalid(string message) { return new Result(ErrorCode.NotRunning, message); }
        private void SafeStop() { try { backend.Stop(); } catch (Exception) { /* Preserve the originating error. */ } }
        private void ClearFrame() { Frame = default(RuntimeFrame); TriggerPressed = false; triggerWasHeld = false; }
    }
}

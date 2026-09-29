using System;
using SpatialAI.Core;

static class Program
{
    private static int checks;
    static void Check(bool condition, string name)
    { if (!condition) throw new Exception("FAIL: " + name); checks++; }
    static int Main(string[] args)
    {
        try
        {
            var clock = new FakeClock();
            var simulator = new SimulatorBackend(clock);
            var session = new SpatialSession(simulator);
            Check(session.Tick().Code == ErrorCode.NotRunning, "read before start rejected");
            Check(session.Start().Success && session.State == SessionState.Running, "simulator starts");
            Check(session.IsSimulated && (session.Capabilities & Capability.StereoDisplay) == 0, "no fake stereo capability");
            simulator.SetInput(Rotation.FromYawPitch(45, 10), Rotation.Identity, true);
            Check(session.Tick().Success && session.TriggerPressed, "trigger rising edge");
            Check(session.Frame.Head.TimestampNs == 123456789, "monotonic timestamp forwarded");
            session.Tick(); Check(!session.TriggerPressed, "held trigger does not repeat");
            Check(session.Recenter().Success, "recenter succeeds");
            session.Tick(); Check(Math.Abs(session.Frame.Head.Orientation.YawDegrees) < 0.001f, "yaw reset with pitch preserved");
            Check(Math.Abs(session.Frame.Head.Orientation.X) > 0.01f, "recenter preserves pitch");
            session.Suspend(); Check(!session.Frame.Head.IsTracked && session.State == SessionState.Suspended, "suspend clears stale poses");
            session.Start(); session.Tick(); Check(!session.TriggerPressed, "resume clears held input");
            Check(!simulator.SetInput(new Rotation(float.NaN, 0, 0, 1), Rotation.Identity, false).Success, "reject nonfinite pose");
            session.Dispose(); session.Dispose(); Check(session.State == SessionState.Disposed && !session.Start().Success, "idempotent dispose");
            foreach (HardwareTarget target in Enum.GetValues(typeof(HardwareTarget)))
            {
                using (var pending = new SpatialSession(new PendingHardwareBackend(target)))
                    Check(pending.Start().Code == ErrorCode.Unsupported && pending.State == SessionState.Error && !pending.IsSimulated, "unsupported " + target);
            }
            var faulty = new FaultyBackend();
            using (var failure = new SpatialSession(faulty))
            {
                failure.Start(); failure.Tick(); Check(failure.Frame.Head.IsTracked, "fault test valid first frame");
                faulty.Fail = true;
                Check(!failure.Tick().Success && !failure.Frame.Head.IsTracked && failure.State == SessionState.Error && faulty.Stops == 1,
                    "disconnect invalidates frame and releases backend");
            }
            var invalid = new OpticsProfile { ProfileId = "old", DeviceModel = "RhinoXPro", CalibrationVersion = "reference", MeshAssetHash = "reference", EyeWidth = 2160, EyeHeight = 2160, IpdMetres = 0.062f };
            Check(invalid.Validate().Code == ErrorCode.Unsupported, "legacy optical reference is not production calibration");
            invalid.IpdMetres = float.NaN; Check(invalid.Validate().Code == ErrorCode.InvalidArgument, "invalid IPD blocked");
            var reference = new OpticalProfileDescriptor { schemaVersion = 1, profileId = "rhinox-reference",
                deviceModel = "RhinoXPro", deviceIds = new[] { "RhinoX" },
                calibrationVersion = "unverified", eyeWidth = 2160, eyeHeight = 2160, ipdMetres = 0.062f };
            OpticalProfileDescriptor selected;
            Check(DeviceProfileSelector.Select(new DeviceIdentity { DeviceCode = "rhinox" },
                new[] { reference }, out selected).Code == ErrorCode.InvalidArgument && selected == reference,
                "exact device ID selects reference but incomplete optics fail validation");
            Check(DeviceProfileSelector.Select(new DeviceIdentity { DeviceCode = "unknown" },
                new[] { reference }, out selected).Code == ErrorCode.Unsupported && selected == null,
                "unknown device has no fallback optics");
            Check(DeviceProfileSelector.Select(new DeviceIdentity { DeviceCode = "RhinoX", HeadsetId = "external-serial" },
                new[] { reference }, out selected).Code == ErrorCode.Unsupported,
                "external headset ID takes precedence over phone ID");
            Check(DeviceProfileSelector.Select(new DeviceIdentity { DeviceCode = "RhinoX" },
                new[] { reference, reference }, out selected).Code == ErrorCode.InvalidArgument && selected == null,
                "ambiguous profile match rejected");
            var calibrated = new OpticalProfileDescriptor { schemaVersion = 1, profileId = "test-only",
                deviceModel = "TestDevice", deviceIds = new[] { "test-device" },
                calibrationVersion = "test", validatedForHardware = true,
                eyeWidth = 100, eyeHeight = 100, ipdMetres = 0.06f, meshAssetHash = "test-hash" };
            Check(DeviceProfileSelector.Select(new DeviceIdentity { DeviceCode = "TEST-DEVICE" },
                new[] { calibrated }, out selected).Success && selected == calibrated,
                "complete validated profile resolves exact ID without case dependence");
            calibrated.validatedForHardware = false;
            Check(DeviceProfileSelector.Select(new DeviceIdentity { DeviceCode = "test-device" },
                new[] { calibrated }, out selected).Code == ErrorCode.Unsupported && selected == calibrated,
                "matched but uncalibrated profile remains unavailable");
            var displayProfile = new OpticalProfileDescriptor { schemaVersion = 1,
                profileId = "rhinox-pro-legacy-reference", deviceModel = "RhinoXPro",
                screenWidth = 4320, screenHeight = 2160, eyeWidth = 2160, eyeHeight = 2160,
                ipdMetres = 0.062f, frustumNearMetres = 0.1f,
                frustumLeftMetres = -0.07968503f, frustumRightMetres = 0.07968503f,
                frustumTopMetres = 0.07348122f, frustumBottomMetres = -0.08588884f,
                meshId = "rhinox-pro-sharp-2k-separate", meshAssetHash = "sha256:reference" };
            Check(displayProfile.ValidateDisplayConfiguration().Success, "complete display profile accepted");
            displayProfile.screenWidth = 4000;
            Check(!displayProfile.ValidateDisplayConfiguration().Success, "mismatched eye and screen resolution blocked");
            displayProfile.screenWidth = 4320;
            displayProfile.frustumTopMetres = float.NaN;
            Check(!displayProfile.ValidateDisplayConfiguration().Success, "invalid frustum blocked");
            if (Array.IndexOf(args, "--native") >= 0)
            {
                uint mask; var native = NativeRuntimeProbe.Check(out mask);
                Check(native.Code == ErrorCode.Unsupported && native.Message.StartsWith("Native ABI loaded") && mask == 0, "managed/native ABI integration");
            }
            Console.WriteLine("PASS: " + checks + " core contract checks."); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
sealed class FakeClock : IClock { public long NowNs { get { return 123456789; } } }
sealed class FaultyBackend : IRuntimeBackend
{
    public bool Fail; public int Stops;
    public string Id { get { return "fault-test"; } }
    public bool IsSimulated { get { return true; } }
    public Capability Capabilities { get { return Capability.HeadOrientation; } }
    public Result Start() { return Result.Ok; }
    public void Stop() { Stops++; }
    public Result Recenter() { return Result.Ok; }
    public Result ReadFrame(out RuntimeFrame frame)
    {
        frame = new RuntimeFrame(new PoseSample(Rotation.Identity, 100, true), default(PoseSample), false);
        return Fail ? new Result(ErrorCode.BackendFailure, "Disconnected") : Result.Ok;
    }
    public void Dispose() { }
}
}

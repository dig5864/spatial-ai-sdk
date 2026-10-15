# Hand inference scheduling

The SDK defaults to CPU inference and `SpatialHandMode.Single` (fixed, at most one hand).
On a `SpatialHandTracking` component, the Inspector exposes **Inference Backend**
(CPU/GPU) and **Hand Mode** (Single/Dual/Auto). Edit-mode values are saved in the scene.
The first component's Awake applies them unless that option was explicitly set through
the C# API. Later components/scenes preserve the active global configuration.
In Play mode the Inspector shows and changes the effective global settings, without
saving runtime changes to the scene. Backend/mode changes can briefly interrupt tracking.

```csharp
using SpatialAI.Unity;
SpatialHandTracking.InferenceBackend = SpatialHandInferenceBackend.CPU; // or GPU
SpatialHandTracking.HandMode = SpatialHandMode.Single; // or Dual / Auto
```

Configure before enabling tracking, or change these properties on Unity's main thread
at runtime. API overrides are global for the current app session, not persisted preferences.
Dual means up to two hands and still returns results when only one is visible.
The existing `SchedulingMode` and `MaxHands` APIs remain compatible; their setters and
`HandMode` control the same configuration. Auto ignores MaxHands for dynamic scheduling.

Opting into Auto enables the following behavior:
Auto keeps two VIDEO GestureRecognizer instances (one and two hands) on the existing
inference consumer thread. Each camera frame runs only one task. Probe results use
the same JSON publication and downstream identity association as ordinary results.

- Single mode: a two-hand probe every 2 seconds after the previous failed probe completes, prioritizing uninterrupted single-hand tracking.
- First two-hand result: confirm using the next accepted camera frame, without waiting another probe interval.
- Confirmed dual mode: use the two-hand recognizer on every accepted frame.
- Fewer than two hands, including no hands, for 500 ms: return to single mode.
- Probe interval and dual-loss timeout are independent. Second-hand discovery can take up to about 2 seconds plus probe/confirmation processing time; each probe can still cause a brief pause.
- Each instance maintains its own increasing VIDEO timestamp. Modes do not share internal tracking state.
- An inference failure retains the existing CPU HandLandmarker fallback, in fixed mode.

For fixed comparisons:

```csharp
SpatialHandTracking.SchedulingMode = SpatialHandSchedulingMode.Fixed;
SpatialHandTracking.MaxHands = 1; // or 2
```

For automatic scheduling:

```csharp
SpatialHandTracking.SchedulingMode = SpatialHandSchedulingMode.Auto;
```

The SpatialXR Demo broadcast accepts `--es schedulingMode Auto` or `Fixed`.
An explicit `maxHands` extra without a schedulingMode extra selects Fixed for
compatibility with existing performance tests. Supplying Auto overrides that behavior.
Results expose `activeMaxHands` (task used for this frame), `schedulingMode`, and
`schedulingState` (state for the next frame). Probe frames may still take 80–100 ms;
automatic mode does not guarantee continuous 30 FPS or transfer tracking state.

Validate one-hand steady tracking, second-hand acquisition, brief occlusion,
complete hand loss, re-entry, and mode/backend changes. Compare latency spikes
and identity continuity as well as average inference time.

## Diagnostic logs

`SpatialHands` INFO logs include:

- `Recognizer ready`: requested scheduling mode and number of recognizer instances.
- `scheduleEvent`: every probe/confirmation and state transition, with frame sequence,
  actual task, detected hand count, before/after state, conversion/inference time,
  and time since that task last started (`taskGapMs`).
- `scheduleStats`: completed-frame counts and inference mean/max for single,
  probe, confirm, and dual roles over the same two-second pipeline window.
  Fixed two-hand tasks count under dual; use `schedulingMode` to distinguish them.
  Empty roles report zero samples.

Input has no additional 33 ms wall-clock throttle: the camera's capture rate and
serial latest-frame consumer pace inference. Pipeline logs report
`inputThrottle=off throttleSkipped=0`; queue replacement remains enabled and is
reported separately as `queueReplaced` when inference cannot keep up with capture.

Optional per-frame DEBUG logs are off by default. Enable them for a short capture:

```powershell
adb shell setprop log.tag.SpatialHandsFrame DEBUG
adb logcat -v threadtime -s SpatialHands:I SpatialHandsFrame:D SpatialUVC:I Unity:I '*:S'
```

Disable per-frame logs after the capture:

```powershell
adb shell setprop log.tag.SpatialHandsFrame INFO
```

Per-frame logs add overhead. Use INFO window statistics for performance comparisons.
All logged inference durations end before these diagnostic log writes.

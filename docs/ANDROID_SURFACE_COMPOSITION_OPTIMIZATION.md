# Android Cardboard gear and surface composition

The Cardboard gear and close icons are rendered in the XR output. Previously an
additional transparent Android Button was placed over the gear to receive input.
On the tested RhinoX Pro, this made the Activity window a second visible layer:
both layers were reported as `CLIENT`, so SurfaceFlinger composed the full XR
surface through the GPU. The actual HWC decision is device-specific.

The calibration component now handles the gear using Unity Input System
EnhancedTouch, without adding a native view. The bridge keeps Cardboard viewer
scanning for scenes without a calibration component. When calibration is open,
the XR display is stopped and restarted on close; the bridge discovers a
calibration component added after initialization as well.

## RhinoX Pro observations

On 2026-11-02, Unity 6.3 demo, 72 Hz display, Follow display, 3x software
headroom, 20 ms fallback prediction, IMU diagnostics hidden, with `qvrd` and
`cnss_diag` stopped, an automated short touch opened calibration, and closing
it restored stereo. The XR surface was reported as `DEVICE` after returning.

Two sequential, approximately 120-second SurfaceFlinger latency captures were
made without per-frame EGL logging or Perfetto: before, 102 of 8,528 intervals
exceeded 25 ms (1.196%); after, 45 of 8,575 did (0.525%). This is an observed
relative reduction of about 56%, not proof that all changes are caused by the
layer removal. Device state, particularly CPU temperature, varied between runs.
Long intervals remained; this change does not claim to fix DispSync behaviour.

Separate short function-probe captures observed a normal-refresh median of
5.88 ms with the additional layer and 2.69 ms after hiding it. Probe overhead
and non-simultaneous runs limit direct comparison.

The Android-phone touch path and behaviour when XR is not running still require
device verification. In particular, the previous native fallback button was
available when XR could not start; EnhancedTouch currently handles the gear only
while XR is running.

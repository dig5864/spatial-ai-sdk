# Changelog

## 0.2.0-preview.6

- Moved Android camera and MediaPipe source to `visor-x/spatial-ai-android`.
- Pinned the bundled Unity AAR to an Android repository commit and SHA-256.

## 0.2.0-preview.5

- Added RhinoX Pro diagnostic optics mesh and design IMU-to-eye offset to the Cardboard XR sample.
- Added an Android RGB Camera2 and MediaPipe hand tracking preview with shared camera service.
- Projected RGB landmarks into eye space using device camera calibration and approximate per-hand depth.
- Added a repeatable Unity 2022 Android APK build script and on-screen tracking timing diagnostics.
- Android hand tracking is a preview; metric hand pose, XR Hands integration, iOS hands, Unity 6 and Tuanjie validation remain open.

## 0.2.0-preview.4

- Made the XR sample camera follow the XR HMD rotation with the Input System Tracked Pose Driver.
- Updated the existing sample scene and scene generator to use the same camera setup.

## 0.2.0-preview.3

- Clarified iOS host identity fallback and external headset ID boundary.
- Added iOS Cardboard XR setup and Xcode project export to the repository sample project.

## 0.2.0-preview.2

- Added exact device ID to optical profile selection and Android host identity reading.
- Added an optional Cardboard XR bridge and Hello Spatial XR sample for a stereo/head tracking baseline.
- Bundled RhinoX Pro legacy reference metadata for diagnostics only; calibration and OST distortion remain unverified.

## 0.1.0-preview.1

- Runtime contracts, session lifecycle, explicit simulator and pending hardware backends.
- Unity host, build check and Hello Spatial simulator scene generator.
- Native ABI probe and host contract checks.
- Hardware display, XR provider, Cardboard and RhinoX Pro integration remain pending.

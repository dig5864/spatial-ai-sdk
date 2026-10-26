# Changelog

## Unreleased

- Android 屏幕物理尺寸估算、双轴 50 mm 尺子校正和按机型保存；固定支架毫米区域映射到动态输出分辨率。
- 整体提交手机双眼显示配置并确认原生应用版本；独立测试 APK 与支架配置导出复用。

## 0.2.0-preview.11

- 同一份 UPM 包兼容 Unity 2022.3 与 Unity 6；XR Interaction 按包版本适配 XRI 2/3 的命名空间和输入读取。
- Unity 6 保留编辑器配套的 Android 工具链与目标 SDK；Unity 2022 保留 API 34、Build Tools 30.0.3 和 R8 修复。
- 显示、手骨架与交互示例使用随包保留的 Built-in/URP unlit shader；Unity 6 Android 配置使用 Input System。
- 新增跨编辑器的交互、USB Manifest、Gradle 幂等、渲染像素及 Android 构建验证入口。

## 0.2.0-preview.10

- 默认 CPU / VIDEO 推理，可配置 GPU；UVC 身份与显示补偿由 SDK 统一处理。
- 提供独立显示/3DOF 示例、Android XR 配置和构建回调，发布消费者不依赖开发仓库路径。
- 配套 XR Hands / XR Interaction 发布包、标准手势资源、三项 FPS 与安装升级文档。

## 0.2.0-preview.9

- Switched gesture classification to image mode after the RhinoX device's video graph failed when hands appeared.
- Added a one-time Hand Landmarker fallback so a classifier failure cannot repeatedly stall hand tracking; frames report `gestureSource`.
- Combined XR and hand status into one dark stereo panel to keep text readable over scene geometry.

## 0.2.0-preview.8

- Added MediaPipe gesture categories and confidence to each `SpatialHand` while retaining landmark based pinch and sample selection.
- Replaced the Android Unity bridge's separate hand landmarker with one Gesture Recognizer inference path for up to two hands.
- Reworked the XR sample's stereo status panels to show left and right gesture results without overlapping diagnostic text.

## 0.2.0-preview.7

- Added two head-locked pinch targets to the XR sample to verify frame events, hand projection, pointer hover and selection.
- Fixed `SpatialHand.Pinching` on incomplete landmark arrays.
- Unity XR Hands/XRI provider integration is not included in this preview.

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

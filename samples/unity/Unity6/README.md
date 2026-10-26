# Unity 6 默认 IMU Demo

用户工程：`D:\code\unity\VisorX-Demo-unity6`（6000.6.2f1 / URP）。

默认场景为 `Assets/Scenes/ImuDiagnostics.unity`，应用显示名称为 **Spatial-XR-Unity6**。
场景位于构建列表第一项，原有场景保留。Editor Play 默认进入 IMU 场景；`ProjectSettings/SpatialUnity6ImuDemo.json` 保存该选择，Editor 支持脚本在重启后恢复它。

场景复用 `samples/unity/SpatialXR/Assets/SpatialXR/` 中的 `ImuDiagnostics.cs`、`ImuFramePacingProbe.cs` 和 `SpatialFrameRateDebugControls.cs`，包括：

- Cardboard 双目桥接及标准 TrackedPoseDriver 旋转跟踪。
- 世界固定 XYZ 轴、20 / 40 cm 尺度方块、20 cm 棋盘格。
- IMU/FPS 诊断面板、屏幕尺子/校准设置、点击/触发器/R 归零。
- 右上角齿轮内的帧率设置：锁定 60 FPS、跟随屏幕刷新率、2x/3x/4x 软件余量。面板和偏好保存属于 Demo，帧率策略及状态来自 SDK。
- Editor 中预览参照物，不伪造 HMD 姿态；无设备时显示 XR WAIT / Pose unavailable。

原 Demo 中直接访问旧 Input 的路径已经为 Unity 6 分支适配。Unity 6 使用 Input System 的 Gyroscope / Accelerometer 与指针/键盘；旧输入重置只在启用 Legacy Input Manager 时编译。Input System 的陀螺仪为 angularVelocity，不声称等同于旧 API 的 rotationRateUnbiased。

## 在已有 Unity 6 消费工程中生成

先安装 SDK、配套 Cardboard 及 XR 依赖，再执行：

```powershell
python -X utf8 tools/PrepareUnity6ImuDemo.py 'D:/code/unity/VisorX-Demo-unity6' --request
```

已打开的 Editor 刷新（Ctrl+R）后会自动生成和配置。也可执行菜单 **Spatial AI > Unity 6 > Use IMU Default Scene**。
生成器保留已存在的 IMU 场景；有未保存的场景时保留当前编辑工作。原 Product/Build Settings 备份位于目标工程 `Library/SpatialUnity6ImuSetup/`。
重新执行会为已有 IMU 场景补挂缺失的帧率设置组件。Android Optimized Frame Pacing 显式关闭，与原 SDK IMU Demo 保持一致。
没有改动 Android Application Identifier。Android 配置保持 ARM64、IL2CPP、GLES3、最低 API 28，使用 Unity 6 配套 Android 工具链。

构建菜单：**Spatial AI > Unity 6 > Build IMU APK**，输出为目标工程 `Builds/Android/Spatial-XR-Unity6.apk`。

## 验证

6000.0.84f1 Built-in 和 6000.6.2f1 URP 均通过默认设置、场景 Play、Input System R 键归零及参照物 GPU 渲染检查；另验证重新启动 Editor 后默认 Play 场景可恢复。
日志位于 `build/validation/unity6-compat/{6000.0,6000.6-urp}/imu-*.log`，渲染图位于各工程 `Library/ImuDiagnostics-preview.png`。
6000.6.2f1 URP 的 IMU 验证 APK 已生成于 `build/validation/unity6-compat/6000.6-urp/Builds/Android/Spatial-XR-Unity6.apk`；aapt2 核对应用标签为 `Spatial-XR-Unity6`。该隔离验证工程沿用包名 `com.spatialai.compatibility`；用户 Demo 原有包名保留。Unity 2022.3 原 IMU 分支编译检查通过。
真机 IMU 跟随、光学合相和物理尺度仍需设备验收。

2026-10-26 已补齐 Unity 6 帧率面板；6000.0 和 6000.6 均通过场景组件及 SDK 策略切换检查。`FollowDisplay` 跟随系统实际模式，不会强制 120 Hz；软件上限 120 也不代表实测 120 FPS。残留卡顿排查见 `docs/UNITY6_IMU_PACING_SYNC.md`。

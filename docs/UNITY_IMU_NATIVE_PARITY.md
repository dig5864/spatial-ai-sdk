# Unity IMU 原生基线迁移

帧率设置现已统一为 SDK 运行时配置，使用 `SpatialFrameRate.Configure` 选择 `FollowDisplay` / `Locked60` 和 2～4 倍软件余量；Demo 齿轮面板通过同一 SDK API 设置。标准构建入口为 `ImuPacingComparisonBuild.BuildSdkFrameRateSettings`，不再使用帧率编译宏。完整用法见 [SDK 帧率配置](FRAME_RATE_CONFIGURATION.md)。

此前用户动作验证的基线为 `unity-v4-async-display-poll`（2026-10-26，小米 mayfly），验收目录 `build/validation/imu-pacing/unity-pipeline-v4-async-display-poll-20261026-113624`：3662 个呈现间隔无长间隔，P99 8.2411 ms，用户反馈目前未发现卡顿。下文 v1～v4 的入口和记录仅为历史对照，相关 APK 仍在本机归档；当前统一配置入口见上方文档。

版本：`unity-native-parity-v1`。迁移原生 v6 的显示模式选择、实际刷新率适配、预计呈现时刻姿态预测和同次查询诊断。当前仅在手机参考畸变模式下启用，由 `SpatialCardboardBridge.useDisplayTimedFramePacing` 控制。

- 支持同分辨率 120 Hz 时优先请求；目标帧率以实际生效刷新率计算。60/90/120/144 Hz 分别使用 60/90/120/144 FPS，240 Hz 使用 120 FPS。模式变化后更新。
- Android 13+ 的 Choreographer 时间线通过 JNI 发布到 XR 输入插件。每次原有姿态查询使用最新预计呈现时刻，转换 MONOTONIC 到 BOOTTIME；过期目标钳制为当前时刻。无接口或无有效时间线时回退 50 ms。
- 保留 Unity 原有 Tick/BeforeRender 查询流程，不增加额外姿态查询；优化帧同步沿用此前 B 配置（关闭）。光学参数与坐标映射沿用基线。
- 系统预计呈现时间并未绑定 Unity 的某个已提交缓冲区，不能当作实测 motion-to-photon 延迟。帧率是否达标由 SurfaceFlinger 实际呈现间隔确认。

## 构建

在仓库根目录执行 `python -X utf8 tools/BuildUnityImuTiming.py --install`，然后使用 Unity 菜单 `Spatial AI/IMU Pacing/Build Native Parity - Display Timed`，或批处理入口 `ImuPacingComparisonBuild.BuildNativeParity`。

当前构建工具使用本机 Unity 2022.3.14f1 内置 NDK/JDK、`D:/android_sdk` Android 34。生成目录为 `build/native/unity-imu-timing`，`build-info.json` 记录二进制与源文件哈希。编辑器依赖钩子在辅助 AAR 存在时重建时序插件，避免被旧构建脚本覆盖。

原始 Cardboard AAR 保存在 `build/native/unity-imu-timing/baseline-cardboard.aar`，原生对照包构建仍使用它并验证原 SHA256。Unity 修改在隔离 SDK 副本中应用，不覆盖共享 SDK 源码。

## 采集

定位 Unity 卡顿时，双击 `tools/CaptureUnityImuDiagnostics.cmd`。默认设备 `10.168.2.127:43495`，采集 30 秒，并额外记录 Perfetto 的 gfx/view、线程调度、CPU 频率和 SurfaceFlinger FrameTimeline（以设备支持情况为准）。无需 Unity Profiler 或 Development Build。

开始前保持 Unity IMU 场景在前台；看到 `Capturing` 后重复左右转动、快速换向和停下。采样结束后等待 `Complete with pipeline trace`，再按任意键关闭窗口。跟踪包含前后上下文，因此等待时间会超过 30 秒；Wi-Fi 下载也需额外时间。结果目录中的 `pipeline.perfetto-trace`、`pipeline-config.txt`、`pipeline-capture.txt` 和原有量化/姿态日志请一起保留。

自定义设备或时长可执行 `tools\CaptureUnityImuDiagnostics.cmd --serial IP:端口 --seconds 30`。命令行入口为 `python -X utf8 tools/CaptureImuDiagnostics.py --package com.spatialai.samples.xr --label unity-pipeline-v1 --seconds 30`；可通过 `--adb` 指定 adb.exe 路径。脚本也会尝试本机 `D:/android_sdk/platform-tools/adb.exe`。

手机打开 Unity IMU 场景后运行（adb 需在 PATH 中）：

```powershell
python -X utf8 tools/CaptureImuPacing.py --serial 10.168.2.127:43495 --package com.spatialai.samples.xr --label unity-native-parity-v1 --seconds 30 --device-poll
```

连续缓慢转动、快速转动、反向和停住。结果位于 `build/validation/imu-pacing`。采集脚本自动读取实际目标帧率，不需写死 120。`quantitative.md/json` 包含实际呈现节奏和 Unity 姿态统计；`unity-pose.csv` 记录查询阶段、预测时长、样本年龄、过期/回退来源。查询次数不等于渲染帧数。

日志标签为 `SpatialUnityTiming` 和 Unity `SpatialAI FRAME-TIMING`。调试 UI 仍可通过右上角齿轮开启。

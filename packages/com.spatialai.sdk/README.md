# Spatial AI SDK — 0.2.0-preview.4

这是可导入的预览框架，提供纯 C# Core、Unity Host、构建预检、模拟示例、设备 ID 到光学 Profile 的匹配，以及可选的 Cardboard XR 基线 Sample。Cardboard XR Plugin 负责其自身的双目显示和头姿；**RhinoX Pro 光学与独立手柄尚未接入**。

通过 Package Manager 的 Add package from disk 选择本目录 package.json。导入 Hello Spatial (Simulator) Sample，再使用菜单 Spatial AI > Create Simulator Scene；保存场景，进入 Play。用头姿与手柄滑条改变方向，目标变黄后按住 Trigger，Hits 增加一次；松开再按可重复触发。

模拟 Sample 不需要启用 XR provider。场景使用屏幕画面演示接口，不能代替双目或硬件验收。

## Cardboard XR 基线 Sample

仓库内的消费工程位于 `samples/unity/SpatialXR/`，Unity 2022 示例使用 Cardboard XR Plugin v1.30.1（其 UPM 包版本号为 1.30.0）。导入 `Hello Spatial XR (Cardboard baseline)`，创建 Sample 场景，并在 Android 的 XR Plug-in Management 中启用 Cardboard。`SpatialAI.Cardboard` 是条件编译的适配层；没有 Cardboard 包时不参与编译。Sample 的 Camera 使用 Input System Tracked Pose Driver 读取 XR HMD 旋转，Cardboard 的 XR Display/Input 子系统负责双目、Viewer 参数与畸变；Spatial AI 只读取设备 ID、匹配自有 Profile 并显示标定状态。

Android 身份读取 `Build.MANUFACTURER`、`MODEL`、`DEVICE`；iOS 读取 Unity 提供的手机型号。外接眼镜 ID 须由未来的设备连接服务填入，不使用手机 ID 冒充外接眼镜 ID。精确匹配失败或 Profile 未验证时返回错误；已收录的 RhinoX Pro 旧数据只供诊断，未应用于 Cardboard 渲染。该 Sample 的注视与 Cardboard 触发不等于 3DoF 手柄。消费工程同时提供 Android 与 iOS 的 Cardboard XR Loader 配置；iOS 构建仍需在 macOS 上导出 Xcode 工程并完成真机验证。

Unity 6 与团结引擎是支持目标，准确版本仍待实测；当前消费工程使用 Unity 2022.3.14f1c1 编译基线。

旧 `SpatialRuntimeHost` 如选择 Cardboard/RhinoXPro 仍返回 Unsupported；Cardboard 基线通过独立的 `SpatialCardboardBridge` 进入 XR 插件，不伪装成已经接入 `SpatialSession` 的硬件 Backend。

## 手部跟踪预览

`Plugins/Android/spatial-hand-unity.aar` 封装 Camera2 与 MediaPipe，Unity 侧通过
`SpatialAI.Unity.SpatialHandTracking` 获取归一化图像坐标中的 21 点及原始手别标签。
`HelloSpatialXR` 示例显示诊断骨架和捏合次数。RhinoX 参考配置采用设备提供的
1440×1080 RGB 内参及 8 项畸变系数，将图像点还原成相机射线。只有 Camera2 ID 为
`2` 且实际帧尺寸为 1440×1080 时才显示该骨架；尺寸或镜头不匹配会在画面上提示。
目前骨架放在示例假定的 0.65 m 深度，**尚未完成 RGB→眼中心外参，也没有手部绝对深度**，
不能用它判断最终的 AR 空间对位。设备上的 `/backup/aio/rgb_vio_camera_params.xml`
可能覆盖 `/backup/rgb_vio_camera_params.xml`；还需要当前设备的
`/backup/slam/device_calibration.xml`，才能沿旧版 `CameraParamLoader` 的矩阵链计算眼中心外参。
Android 构建时包内的 Gradle 后处理器会加入固定版本
`com.google.mediapipe:tasks-vision:1.0.0`；手势预览最低需要 Android API 28。

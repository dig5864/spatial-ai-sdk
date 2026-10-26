# Spatial AI SDK — 0.2.0-preview.11


## 发布包快速接入

在 Package Manager 中通过 Add package from tarball 导入 `com.spatialai.sdk-0.2.0-preview.11.tgz`；基础包自动安装内置的 Spatial 修改版 Cardboard、XR Hands 和 XR Interaction，无需独立 installer。
运行 **Spatial AI > Setup > Configure Android XR**，设置自己的 Application Identifier，将示例加入 Build Settings，再构建 APK。
用户不需要检出开发仓库、复制 Demo 构建脚本或编译 AAR。发布包含生成的 Android AAR 与模型，源码 Git 继续忽略 AAR。

纯显示使用 **Spatial AI > Display > Create Display and 3DOF Scene** 或导入 Display and 3DOF Sample；它不创建摄像头或手势推理。
应用获取手部、跟踪和手势使用标准 XRHandSubsystem、XRHandTrackingEvents、XRHandShape/XRHandPose、官方 StaticHandGesture 和 XRI。
旧图像坐标与 MediaPipe 类别接口仅保留兼容和诊断用途。
完整安装、示例、兼容矩阵及升级步骤见 [安装说明](Documentation~/INSTALL.md)，补偿策略见 [手部补偿](Documentation~/UVC_HAND_SDK_COMPENSATION.md)。
默认 CPU / VIDEO，可通过 SDK 后端配置切换 GPU；标准 XR Hands 接口没有推理后端配置项。

这是可导入的预览包，提供纯 C# Core、Unity Host、模拟示例、设备 ID 到光学 Profile 的匹配，以及 Cardboard XR Sample。RhinoX Pro 光学网格和 IMU 设计偏移已接入 Android 诊断路径，尚未完成逐台光学校准；独立 3DoF 手柄尚未接入。

通过 Package Manager 的 Add package from disk 选择本目录 package.json。导入 Hello Spatial (Simulator) Sample，再使用菜单 Spatial AI > Create Simulator Scene；保存场景，进入 Play。用头姿与手柄滑条改变方向，目标变黄后按住 Trigger，Hits 增加一次；松开再按可重复触发。

模拟 Sample 不需要启用 XR provider。场景使用屏幕画面演示接口，不能代替双目或硬件验收。

## Cardboard XR 基线 Sample

仓库内的消费工程位于 `samples/unity/SpatialXR/`，Unity 2022 示例使用 Cardboard XR Plugin v1.30.1（其 UPM 包版本号为 1.30.0）。导入 `Hello Spatial XR (Cardboard baseline)`，创建 Sample 场景，并在 Android 的 XR Plug-in Management 中启用 Cardboard。`SpatialAI.Cardboard` 是条件编译的适配层；没有 Cardboard 包时不参与编译。Sample 的 Camera 使用 Input System Tracked Pose Driver 读取 XR HMD 旋转，Cardboard 的 XR Display/Input 子系统负责双目、Viewer 参数与畸变；RhinoX Android 诊断版另外载入历史光学网格与 IMU 设计偏移。

Android 身份读取 `Build.MANUFACTURER`、`MODEL`、`DEVICE`；iOS 读取 Unity 提供的手机型号。外接眼镜 ID 须由未来的设备连接服务填入，不使用手机 ID 冒充外接眼镜 ID。RhinoX Pro 诊断网格使用旧硬件数据，尚未完成逐台光学等效验收。该 Sample 的注视与 Cardboard 触发不等于 3DoF 手柄。消费工程同时提供 Android 与 iOS 的 Cardboard XR Loader 配置；iOS 构建仍需在 macOS 上导出 Xcode 工程并完成真机验证。

Unity 6 与团结引擎是支持目标，准确版本仍待实测；当前消费工程使用 Unity 2022.3.14f1c1 编译基线。

旧 `SpatialRuntimeHost` 如选择 Cardboard/RhinoXPro 仍返回 Unsupported；Cardboard 基线通过独立的 `SpatialCardboardBridge` 进入 XR 插件，不伪装成已经接入 `SpatialSession` 的硬件 Backend。

## 手部跟踪预览

`Plugins/Android/spatial-hand-unity.aar` 封装 Camera2 与 MediaPipe；源码与构建脚本位于独立的 `visor-x/spatial-ai-android` 仓库，当前二进制的提交和 SHA-256 记录在 SDK 仓库的 `third_party/spatial-ai-android.lock.json`。Unity 侧通过
`SpatialAI.Unity.SpatialHandTracking.FrameUpdated` 获取归一化图像坐标中的 21 点、原始手别标签、`SpatialHand.Gesture` 类别与 `gestureScore` 置信度。当前类别包括握拳、张掌、食指向上、拇指向上／下、剪刀手和“我爱你”；无法分类时为 `None`。`Pinching` 仍由关键点计算，Demo 的 `Select` 仍需指向目标后捏合。
`SpatialHandFrame.gestureSource` 为 `mediapipe` 时提供模型分类；若设备上的分类器报错则自动改为 `landmarks-fallback`，继续输出手骨架与捏合，但不再产生分类结果。
`HelloSpatialXR` 示例为左右手分别显示分类和置信度，并保留诊断骨架与捏合次数。RhinoX 参考配置采用设备提供的
1440×1080 RGB 内参及 8 项畸变系数，将图像点还原成相机射线。只有 Camera2 ID 为
`2` 且实际帧尺寸为 1440×1080 时才显示该骨架；尺寸或镜头不匹配会在画面上提示。
已读取测试机 `03D11C420163` 的 `/backup/slam/device_calibration.xml` 与
`/backup/rgb_vio_camera_params.xml`，并按旧版 `CameraParamLoader` 的矩阵链和 RhinoX Pro
设计 Eye→IMU 值计算 RGB→眼中心矩阵。该机 `/backup/aio/rgb_vio_camera_params.xml` 不存在。
示例根据去畸变后的手掌宽度与长度估算每只手的深度，估算失败时回退到 0.35 m；单目 MediaPipe 没有手部绝对深度，估值会随实际手掌尺寸和手掌朝向变化，
设备标定文件中的 `deviceUID` 也为 `not_configured`。外参目前只作为这台测试机的候选值，
必须通过镜中实测对位后才能标记为已验证；其他设备应读取并转换各自的标定文件。
手势推理输入按原始 1440×1080 的宽高比缩至 384×288，最短取帧间隔为 33 ms；
画面显示实际结果 FPS、像素转换耗时、模型推理耗时，以及相机时间戳可用时的采集到结果耗时。
这几个数值用于判断是否还需要 GPU 推理或更低分辨率，不能把取帧间隔当成实际帧率。
Android 构建时包内的 Gradle 后处理器会加入固定版本
`com.google.mediapipe:tasks-vision:1.0.0`；手势预览最低需要 Android API 28。

## 手追踪推理配置

SDK 默认 CPU、固定单手。在场景的 `SpatialHandTracking` 组件 Inspector 中设置：

- `Inference Backend`：CPU / GPU。
- `Hand Mode`：Single / Dual / Auto。

编辑模式保存启动配置，运行模式显示并修改当前全局配置，不保存运行时的修改。
首次组件初始化时应用 Inspector 配置；该选项若已由 C# API 显式设置，则 API 优先。
后续场景中的组件不会覆盖已有的全局配置。

```csharp
using SpatialAI.Unity;

SpatialHandTracking.InferenceBackend = SpatialHandInferenceBackend.CPU;
SpatialHandTracking.HandMode = SpatialHandMode.Single;
// 可切换为 SpatialHandInferenceBackend.GPU，以及 SpatialHandMode.Dual / Auto。
```

在 Unity 主线程中设置，可在追踪启动前配置或运行时切换。切换可能短暂中断识别。
Dual 表示最多两只手，Auto 会周期探测第二只手并动态切换；Single 不执行双手探测。
原有 `MaxHands` 和 `SchedulingMode` 接口保持兼容，与 `HandMode` 操作同一配置。
配置是当前进程的全局设置；应用如需记住最终用户的选择，应自行保存并在下次启动时应用。

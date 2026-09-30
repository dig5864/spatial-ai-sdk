# visor-x Group 与 Spatial AI SDK 说明

**GitLab Group ID：139**

## Group 定位

Visor-X 围绕手机计算的低成本 AR 应用平台开展研发。硬件是应用入口，SDK 是开发者入口；长期建设方向包括 Spatial Runtime、Spatial AI 能力与空间工具接口、应用分发和商业服务。当前以 RhinoX Pro 开发设备验证 Android 与 Unity XR 链路。尚未验证的手机、头显和平台组合不视为正式兼容。

建议填写到 GitLab Group「Description」的简介：

> Visor-X / Spatial AI SDK 研发空间。围绕手机计算的低成本 AR 应用平台，维护 Android 设备能力、Unity XR SDK、示例工程和技术文档；后续扩展 Unity 6、团结、WebXR、OpenXR 与 Native。当前以 RhinoX Pro 开发设备验证，其他手机与硬件组合仍待适配。

## 仓库分工

**[spatial-ai-sdk](http://gitlab.ximmerse.com/visor-x/spatial-ai-sdk)** 是主 SDK 仓库，维护跨平台能力契约、Unity UPM 包、Cardboard 与 RhinoX XR 适配、Unity 示例工程、构建脚本和文档。当前预览包为 `0.2.0-preview.9`，已在 Unity 2022.3.14f1c1 与 RhinoX 开发设备上构建、安装和测试。Unity 6、团结及 iOS 真机尚未验收。

**[spatial-ai-android](http://gitlab.ximmerse.com/visor-x/spatial-ai-android)** 维护 Android CameraService、MediaPipe 手部桥接、供 Unity 使用的 AAR，以及相机和手部独立诊断应用。当前通过 RhinoX 内置 Camera2 RGB 相机取帧；UVC 摄像头、Vuforia 和手机端设备适配尚未接入。

**[xms-xr-sdk](http://gitlab.ximmerse.com/visor-x/xms-xr-sdk)** 是历史 XR 工程，供光学参数、畸变网格和旧版 Unity XR 实现参考。当前新功能开发和验收以 `spatial-ai-sdk` 为准。

**[demo](http://gitlab.ximmerse.com/visor-x/demo)** 是 Group 内已有的独立 Demo 项目。本次 SDK 整理不改变其内容与职责；目前 SDK 的验证示例位于主 SDK 仓库。

Android 仓库生成的 Unity AAR 随 SDK 包交付。主仓库的 `third_party/spatial-ai-android.lock.json` 固定对应的 Android 源码提交号、模型校验值和 AAR SHA-256，便于追溯二进制来源。

## 目前完成的功能

### RhinoX Pro 显示、头姿和反畸变预览

Unity XR 示例已通过 Cardboard XR Plugin 接入 Unity 标准 XR 显示与输入链路，实现双目画面和 3DoF 头姿。示例相机使用 Input System Tracked Pose Driver，头姿通过 Unity XR Input 读取。RhinoX Pro 历史 OST 畸变网格及 IMU 到眼睛的设计偏移已合入开发预览，在 RhinoX 开发设备上运行。历史光学参数仍需逐台校准和等效验收；手机连接外置头显的链路尚未开始适配。

### RGB 相机与手部跟踪

Android CameraService 当前从 RhinoX 通过 Camera2 暴露的内置 RGB 相机采集帧，并提供多消费者分发。MediaPipe 最多输出两只手的 21 个关键点和左右手标签。Unity Demo 绘制手骨架，并依据候选相机内外参投到眼空间；手部距离目前由图像尺度估算，不是精确测距。**USB UVC 摄像头尚未合入。**

### 手势接口与交互 Demo

Unity SDK 的 `SpatialHandTracking.FrameUpdated` 提供逐帧手部结果。每只手包含关键点、手别、MediaPipe 手势类别与置信度；SDK 另根据关键点计算 `Pinching`。当前类别包括握拳、张掌、食指向上、拇指向上与向下、剪刀手和“我爱你”。Demo 用指向目标后捏合验证 `Select`，并显示左右手类别、帧率和计数。真机已确认双手可识别，张掌、握拳时类别会变化，修复版佩戴测试画面流畅。分类器异常时会停止重复报错并回退到手部关键点跟踪，保留骨架与捏合能力。

**Unity 接口边界：**双目显示和头部姿态已使用 Unity 标准 XR 链路；手势目前通过 Spatial AI SDK 自有 C# 接口提供。手部关键点尚未注册为 Unity XR Hands 的 `XRHandSubsystem`，捏合与选中也尚未映射到 XR Interaction Toolkit 的标准交互输入。Demo 的 `Select` 是应用交互结果，并非 MediaPipe 内置类别。

当前头姿为 3DoF，**Tag 定位追踪尚未实现**；现有手部跟踪也不提供头显在空间中的定位。

### 开发与构建闭环

主仓库已有可直接用 Unity 打开的消费工程、自动 APK 构建脚本、本地 UPM 候选包和验证文档；Android 仓库已有 AAR 构建与独立诊断脚本。`0.2.0-preview.9` APK 已在 RhinoX 开发设备安装运行。当前有手日志约为一只手 16–17 FPS、两只手 12 FPS；各类别准确率和长时间稳定性尚未量化。具体验证边界见 [验证记录](VALIDATION.md)。

## 待优化与待接入

1. **延时预测与性能：**测量相机采集、推理、Unity 绘制到显示的端到端时延；优化头部与手部预测、抖动抑制以及双手推理帧率。
2. **Android 手机接入：**确定首批兼容手机，处理屏幕、传感器、相机、USB 连接及外置 RhinoX Pro 头显适配；目前没有正式手机兼容列表。
3. **iOS 手机接入：**完成外置头显连接、显示、传感器和相机链路，并进行 iPhone 真机验收。
4. **UVC 摄像头接入：**增加独立 USB UVC 采集适配器，覆盖设备发现、权限、分辨率与帧率选择、图像格式和生命周期；接入统一帧分发后供手势及后续 Vuforia 等算法复用。当前 UVC 采集、Unity 接口和真机测试均未开始。
5. **Tag 定位追踪：**定义 Tag 类型、相机输入与坐标系，接入检测、位姿估计、持续追踪和丢失恢复，再向 Unity 提供定位状态与位姿接口。当前算法、SDK 接口、Demo 和真机验收均未实现。
6. **标定与交互精度：**完成逐台光学标定、RGB 相机到眼睛的外参验证、手部尺度与深度校准，并量化手势准确率。
7. **Unity 标准手部接口：**将关键点适配到 Unity XR Hands，并将捏合、选中等动作适配到 XR Interaction Toolkit；独立 3DoF 手柄也仍待接入。
8. **其他平台：**验证 Unity 6 与团结引擎，后续按产品进度扩展 WebXR、OpenXR 和 Native。
9. **平台商业层：**在开发者闭环稳定后，逐步建设 Spatial Runtime、Spatial AI 能力与空间工具接口，以及应用分发、授权和支付；企业部署再围绕设备、用户、内容、数据和 AI 用量形成订阅服务。

当前阶段的核心验收目标是：外部 Unity 开发者能够导入 SDK，在目标设备上稳定完成双目显示、头部跟踪和基础手势交互。

## 目录与交付约定

`spatial-ai-sdk/runtime/` 放平台无关的 Core、C ABI、会话和设备 Profile；`packages/com.spatialai.sdk/` 是交付给开发者的 Unity UPM 包；`samples/unity/SpatialXR/` 是可直接打开的 Unity 消费工程，含默认 XR/手势、IMU/尺度和光学对比场景。`tests/` 放 Core、原生 ABI 与 Unity API 检查，`tools/` 放构建和校验脚本，`third_party/` 放上游版本与 Android AAR 来源锁定信息，`docs/` 放架构和验证记录。

`spatial-ai-android/runtime/platform/android/camera/` 放 Camera2 采集与帧分发，`runtime/platform/android/hands/` 放 MediaPipe 与 Unity 桥接；`samples/android/` 放相机和手部诊断工程，`tools/` 放 Android 构建脚本。

两个仓库的 `build/` 均为本机编译中间文件、APK、日志或模型缓存，不提交 Git。主仓库的 `dist/` 放本地版本化 UPM 候选包，也不提交 Git；正式交付时另上传制品库。Android 能力先在 Android 仓库实现并验证，再以固定源码提交和校验值更新 Unity 包内 AAR。Unity 示例的打开和构建步骤见 [SpatialXR README](../samples/unity/SpatialXR/README.md)。

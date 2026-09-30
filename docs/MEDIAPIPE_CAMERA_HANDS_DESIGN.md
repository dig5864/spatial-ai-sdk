# Spatial AI SDK：多相机、MediaPipe 手势与 Vuforia 复用设计（评估稿）

日期：2026-09-29。本文保留当时的架构和验证计划。Android RGB Camera2 与 MediaPipe 手部预览现已合入；Vuforia 尚未合入。当前实现与限制见仓库 `README.md` 和 `docs/VALIDATION.md`。

## 1. 当前基线与结论

- 当前 Unity 示例以 Cardboard XR Plugin 提供双目显示和头姿，`SpatialAI.Cardboard` 处理 RhinoX Pro 光学参考参数；XR Camera 是 Unity 渲染相机，不是头显上的图像采集设备。
- `Capability.Camera`、`Capability.HandTracking` 已在 `packages/com.spatialai.sdk/Runtime/Core/Contracts.cs` 定义，但没有相机帧或手部关节数据接口。原生 C ABI 当前只有姿态读取。
- 当前 UPM 包标记最低 Unity 2022.3，不能把本设计视为已经验证 Unity 2021、Unity 6 或团结。手势适配应通过可选程序集隔离 XR Hands / XR Interaction Toolkit 的版本依赖。
- 已连接设备的 Android 相机服务报告 4 个相机设备（三后、一前）。这只能证明 Camera2 列表中的数量和方向，不能推出哪一个是眼镜上的手势相机、是否有同步双目、是否为 UVC，也不能从 `/dev/video*` 数量推断角色。

**建议：**先建立设备侧 `CameraService`，让每个物理图像源只有一个采集所有者。它按设备配置选择相机，并以带时间戳、标定和生命周期的帧向 MediaPipe 与后续 Vuforia 分发。MediaPipe 只作为可替换的手部关键点推理后端。Unity 侧先交付可实际交互的手射线、捏合和 XRI 适配，再评估完整 `XRHandSubsystem` Provider。

## 2. 分层与数据流

```text
头显摄像头 / 手机 Camera2 / UVC
             │
   CameraService：发现、角色映射、独占采集、会话、帧分发
             │                  │                     │
             │              预览/诊断          Vuforia Driver（后续）
             │                                        │
      MediaPipe Adapter                         目标识别/跟踪
             │
   HandFrame：关节、手别、置信度、时间戳、坐标域
             │
   Gesture/Interaction：捏合状态、手射线、丢失策略
             │
 Unity API / XRI 适配 / 可选 XR Hands Provider
             │
 Unity XR Origin + Cardboard 显示/头姿
```

Cardboard 继续负责显示/头姿。相机、手势与 Vuforia 不拥有 XR 显示提交或眼镜畸变网格。对 Unity 应用，`CameraService` 通过 Android 插件暴露稳定接口；帧采集和推理不得阻塞 Unity 主线程。

建议源码落点（按实现进度建目录，不预先生成空模块）：

| 模块 | 建议位置 | 边界 |
| --- | --- | --- |
| 设备相机配置、坐标/时间契约 | `runtime/devices/rhinox-pro/`、`runtime/include/` | 与 Unity、模型无关 |
| Android Camera2/UVC 采集与帧分发 | `runtime/platform/android/camera/` | 一个物理源一个采集所有者 |
| MediaPipe Android 适配 | `runtime/platform/android/hands/mediapipe/` | 只接 `CameraFrame`，只产 `HandFrame` |
| Unity 手势/XRI 桥接 | `packages/com.spatialai.sdk/Runtime/Hands/` | 可选包依赖，不影响现有样例 |
| Vuforia Driver | 独立可选集成包，待可行性验证后定名 | 不进入基础 UPM 包 |
| 诊断与验收 | `samples/android/`、`samples/unity/`、`tests/` | 记录设备与版本 |

## 3. 相机服务契约

### 3.1 发现与角色

启动时枚举 Camera2 ID、逻辑/物理相机关系、方向、格式/分辨率/帧率、硬件时间戳能力和可并发组合；另行枚举 USB 摄像头及权限。用 `deviceId + 硬件修订 + 相机稳定标识` 映射角色，避免把 `0/1/2/3` 写死。

角色至少预留 `HandLeft`、`HandRight`、`SceneRgb`、`TrackingLeft`、`TrackingRight`，一个相机可有多个消费者，但角色并不意味着设备当前具备该相机。配置记录图像朝向、镜像、内参、畸变、`T_head_from_camera`、标定版本和可信状态。先在真机上确认哪个 Camera2/USB 设备真实对应头显镜头；手机前后摄不能直接代替头显手势相机的空间标定。

### 3.2 一次采集，多路消费

`CameraFrame` 至少包含：源 ID、单调时间戳及其时间域、序号、宽高/步幅、像素格式、旋转/镜像、内参版本、曝光信息（若可用）、缓冲句柄与释放规则。消费者按需订阅，拥有独立的最新帧队列与丢帧统计。慢速手势推理不能反压相机或 Vuforia；有界队列优先保留最新帧。

第一版可用 Camera2 `ImageReader` 的 YUV 帧做基线，先测拷贝与格式转换成本，再决定 `HardwareBuffer`/纹理路径。帧在最后一个消费者释放前必须有效；绝不能把已关闭的 `Image` 指针交给异步推理或 Vuforia 回调。相机只开一次，但分发时可能需要格式转换或内存复制，因此“复用相机”不承诺“零拷贝”。

服务统一处理权限、USB 插拔、后台/前台切换、断开重连和消费者启停。多相机是否可同时出流取决于 Android Camera HAL 的逻辑相机/物理相机关系与设备实际并发能力，必须逐组合测试。

## 4. MediaPipe → Unity XR 手势

### 4.1 推理边界

优先评估官方 MediaPipe Tasks Vision `Hand Landmarker` Android API 的 `LIVE_STREAM` 模式，不把用户提供的旧 Android Framework/Bazel 入门页当成唯一集成方案。将版本和模型文件固定在构建中，不使用浮动的 `latest.release`。输入由 `CameraService` 提供，推理结果回调转换为 SDK 自有的 `HandFrame`。

`HandFrame` 保留每手 21 个原始图像关键点、MediaPipe 的相对手部 3D 关键点、左/右手判断及分数、推理输入帧 ID/时间戳、来源相机 ID、跟踪有效性。**MediaPipe 所称 world landmarks 是以手中心为原点的局部米单位结构，不能直接当作眼镜坐标系中的绝对手位置。** 单目相机首先能稳定提供图像平面位置与姿态/捏合线索；若要有可信的眼前距离和双目交互，需另做相机标定、深度估计或同步双目三角化并实测误差。

对 `LIVE_STREAM` 的输入使用原帧时间戳的单调映射；若推理忙而丢帧，显式记录帧号缺口和端到端延迟。只在工作线程进行图像预处理和推理，Unity 主线程读取不可变的最新结果快照。

### 4.2 坐标和 Unity 适配

定义 `CameraPixels → UndistortedCameraRay → HeadLocal → TrackingOrigin/UnityWorld` 的变换链。将图像旋转、镜像、左右手标签校正与坐标手性变换集中在一个模块，不允许每个 Sample 自行翻轴。`T_head_from_camera` 与采集时刻的头姿一起决定手在 Unity 世界的投影；现有头姿仅 3DoF，不能凭空生成稳定的头部平移。

Unity V1 的交互输出建议为：左右手可见状态、稳定的捏合开始/保持/结束、相机标定后可用的手射线。以这些语义驱动 XRI 的 Interactor/Interactable 或 Input System action，让开发者不用依赖 MediaPipe 类型。需定义手离开视野、短时遮挡、左右手身份切换时的取消行为和滞回阈值。

完整 XR Hands Provider 是下一层：它需要把 MediaPipe 21 点映射至 XR Hands 关节约定，补齐/声明缺失关节与方向、提供跟踪状态，并和现有 Cardboard XR Loader 协同注册。不得把未测得的关节姿态或绝对深度标成可靠跟踪。Unity 2022.3、Unity 6、团结各自锁定兼容的 XR Hands/XRI 版本和程序集定义；Unity 2021 保留基础手势 API 方案并单独验证支持矩阵。

## 5. Vuforia 共用相机的接入判断

Vuforia 常规 Unity 相机链路会自行控制设备相机。官方 Driver Framework 支持由 C/C++ 动态库向 Vuforia 输入外部相机帧及内参，Unity 项目可加载该驱动。因此未来应由 `CameraService` 继续拥有物理相机，Vuforia Driver 作为帧消费者注册，把帧、准确的时间戳/曝光/内参交给 Vuforia。不能同时让 Vuforia 默认相机链路与本服务独立打开同一设备。

在真正接入前需做独立 PoC：验证选定 Vuforia 版本及授权、Android/Unity 驱动加载、目标头显相机格式、帧回调内存生命周期，以及 Cardboard XR Camera 与 Vuforia 相机姿态/视频背景是否冲突。目标识别只需图像输入的情况，与需要 6DoF 相机位姿的 Area Target 等能力应分开验收。当前 3DoF 系统不应承诺后者。若 Driver 在该组合不可用，退路是模式切换或分阶段启停相机，不把双开相机作为架构依赖。

## 6. 验证顺序与验收门槛

1. **硬件勘测**：在 RhinoX Pro + 当前安卓手机上，列出每个相机 ID、传感器实际位置、输出格式/帧率、同步能力、标定文件来源、USB 热插拔路径；拍摄遮挡各镜头确认映射。输出设备配置表。当前只有“Android 服务报告 4 个相机”这一初步事实。
2. **CameraService 原型**：选定一枚手势相机，持续采集并在两路独立消费者显示相同帧号与时间戳；停止/重启/拔插/切后台后无悬挂帧和相机占用。记录帧率、丢帧、CPU/GPU、内存与耗电。
3. **MediaPipe 单手到双手**：显示 21 点、手别、置信度和输入帧延迟；实测正常光照/低光/转头/遮挡，确认图像镜像与坐标轴。交互验收为捏合选中、释放、手移出画面后取消，不误触发。
4. **Unity XR 交互**：在现有 SpatialXR Sample 中与双目、头姿同时运行，以 XRI 物体做手射线/捏合交互；分别验证 Unity 2022.3、Unity 6 和团结兼容矩阵，再决定 XR Hands Provider 的交付范围。
5. **Vuforia PoC**：同一个帧源同时供 MediaPipe 与 Driver，核对帧号/时间戳和相机内参，完成至少一个 Image Target 识别；测并行吞吐与延迟，并确认不与 Cardboard 的 XR Camera 抢控制权。

每个阶段都保存真机日志、版本锁、实际帧率及端到端延迟分布；具体性能阈值在硬件勘测后依据可用相机和手机能力确定，不预设未经测量的数字。

## 7. 官方资料

- [用户提供的 MediaPipe Android Framework 入门页](https://chuoling.github.io/mediapipe/getting_started/android.html)
- [MediaPipe Hand Landmarker Android](https://developers.google.com/edge/mediapipe/solutions/vision/hand_landmarker/android)
- [Unity XR Hands：实现 Provider](https://docs.unity.cn/Packages/com.unity.xr.hands@1.4/manual/implement-a-provider.html)
- [Unity 2022 XR 输入概览](https://docs.unity3d.com/cn/2022.3/Manual/xr-input-overview.html)
- [Android 多相机 API](https://developer.android.com/media/camera/camera2/multi-camera)
- [Vuforia Driver Framework](https://developer.vuforia.com/library/vuforia-engine/platform-support/camera/driver-framework/driver-framework/)
- [Vuforia Camera Driver](https://developer.vuforia.com/library/vuforia-engine/platform-support/camera/driver-framework/simple-driver/creating-custom-driver/)

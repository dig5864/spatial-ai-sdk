# UVC 手部补偿与推理后端配置

日期：2026-10-12。旧 `hands` 预览、标准 `xrhands` 和 `xri` 场景共用 SDK 的处理流程。应用获取关节、跟踪事件和手势事件仍使用 Unity XR Hands / XRI 标准接口。

## 默认 CPU 与调试切换

Android MediaPipe 默认使用 **CPU / VIDEO**，最多两只手。配置项决定生成关节点的推理后端，不改变 Unity 官方 `StaticHandGesture` 的形状判断接口。

SDK 的提供者配置：

```csharp
using SpatialAI.Unity;
SpatialHandTracking.InferenceBackend = SpatialHandInferenceBackend.GPU;
// 恢复默认后端：
SpatialHandTracking.InferenceBackend = SpatialHandInferenceBackend.CPU;
```

这是 SDK 配置项，Unity XR Hands 标准 API 没有对应的 CPU/GPU 设置。应用的关节、手势和 UI 交互仍走标准接口；不新增应用手势事件。
可以在启动前或运行中设置。运行中切换清除旧结果与补偿缓存，取消已有跟踪/交互；重新识别后先张开手再交互。
Android 只在相机推理消费者线程创建、执行和释放模型；配置调用不在 Unity 主线程释放 GPU 资源。
显式请求 GPU 时，初始化失败会记录错误并回退 CPU，最终以 `SpatialHands` 日志和结果帧中的实际 `delegate` 为准。

Demo 沿用原广播 action，新增 `inferenceDelegate` 字符串参数：

```bash
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene xri --es inferenceDelegate GPU
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene xri --es inferenceDelegate CPU
```

`scene` 也可使用 `hands` 或 `xrhands`。当前已处于同一场景时只更新配置，不重新加载场景或重开 UVC 摄像头。
新进程默认 CPU；省略 `inferenceDelegate` 保留本进程当前配置。仅接受 CPU/GPU，大小写可忽略；旧的 `gestures`、曝光参数继续可用。

## SDK 统一补偿

基础包中的 `SpatialUvcHandProcessor` 统一调用身份关联、严格空间转换与显示补偿。
`UvcHandsFrameSource` 使用它向 XR Hands 提供有效空间样本，`SpatialXRHandsVisualizer` 自动使用其显示缓存。
旧 `HandTrackingDemo` 也调用同一处理器；不再另建稳定器或维护自己的保留时间、身份和有效距离。
基础包 Samples 中的旧场景脚本与维护工程脚本同步。

| 处理 | 行为 |
| --- | --- |
| 手别稳定 | 在图像中按手腕连续关联，门限 0.18；保持连续身份取得时的标签，原始标签与分数保留 |
| 身份失效 | 250ms 未检测到、较大位置跳变、相机/尺寸/序列/原点/时钟变化时重建 |
| `depth-held` | 同一身份已取得有效距离；掌宽/掌长深度冲突时，用当前关键点配合最近有效相机距离绘制，恢复时重新估距 |
| `held` | 同一身份的几何无效时，最多显示最近有效骨架 150ms |
| `missing-held` | 短暂漏检时，最多显示最近展示骨架 120ms；身份 250ms 保留不等于骨架可显示 250ms |
| 整体过期 | 350ms 没有新结果或收到的结果已经过期时隐藏，渲染阶段检查，不等待下一次推理 |
| 主动失效 | USB/相机失效、推理后端或识别开关变化、暂停、停止时清除身份、距离和展示缓存 |

三个退化状态都只用于 SDK 自带可视化。标准 `XRHandSubsystem` 仍报告未跟踪，不产生新的有效关节姿态，不用保留骨架执行悬停、捏合点击或手势事件。
应用自行使用标准关节绘制时，仍会收到实际失效状态；使用 SDK 可视化组件即可获得上述显示补偿，无需复制场景算法。
显示 X 镜面补偿与解剖手别交换分开处理，遵循主机配置或加载的标定参数。
首次取得身份就识别错误、双手重叠/快速替换仍可能误关联，单目侧掌深度仍不可可靠测量；补偿减少显示闪烁，不能宣称测量精度提升。

## 调试帧率与日志

三个场景显示 `AI`、`UVC`、`Render` 和当前 CPU/GPU：

- AI：Android 完成推理的结果帧率；不是 Unity 轮询次数，也不是显示保留骨架的次数。结果过期后显示 0。
- UVC：实际摄像头采集回调帧率；协商的 `@30` 不等于实际值。
- Render：Unity 场景帧数除以约 1 秒的非缩放时间窗口，双目不重复计数；不是 GPU 耗时。

`SpatialHands` 输出模型就绪、请求后端与实际 delegate；`SpatialUVC` 输出采集与曝光。
SDK 补偿状态变化关键字 `SpatialHandsComp`，包含侧别、状态、序列、身份、距离和拒绝原因。
旧场景保留 `SpatialHandsDiag` 原始标签/分数及关键点快照；它读取 SDK 状态，不再执行另一套补偿。

## 验证与人工检查

自动回归覆盖身份连续性、侧掌实时投影、120/150/350ms 过期、身份变化不得继承距离、标准未跟踪与补偿显示的区别、失效清理、旧场景适配、标准双手点击和生命周期。
实机通过广播检查 CPU → GPU → CPU 的实际日志，测试后恢复 CPU。

佩戴后分别在 `hands`、`xrhands`、`xri` 做“正常掌姿 → 侧掌 → 恢复 → 完全移出 → 返回”，确认左右手不随翻掌交换、保留骨架不误触、恢复后先张开再点击。
检查调试面板中的三项 FPS、镜片可读性，以及 CPU/GPU 下的持续运行表现。
USB 权限 UVC-PERM-001、光学合相与精确标定仍保留为独立待验收项。

## 2026-10-12 本次交付与验证记录

- 阶段 3：109 项契约检查通过，20 次 Play 生命周期循环通过；合成输入覆盖身份稳定、侧掌当前关键点投影、150/120/350ms 到期、头姿只转换一次、缓存失效和标准 Untracked 下的仅显示补偿。
  日志：`build/validation/xr-hands-stage3-f4a1713149764ab9974eb98480a2cae9/verification-sdk-compensation.log`。
- 阶段 4：47 项契约、172 项集成检查通过，20 次生命周期循环、7 次合成 UI 点击通过；保留标准 XR Hands 手势与 XRI 交互、圆圈样式、随头目标、三项 FPS 和 CPU 默认值。
  日志：`build/validation/xr-hands-stage4-2e8da17e8f9d443e84fb14bd033b2287/verification-cpu-compensation.log`。
- 旧 `hands` 场景：191 项集成检查通过，包括共享 SDK 补偿、镜面/手别映射、无手时仍显示推理 FPS、停止来帧后清除骨架与悬停。
  日志：`build/logs/sdk-compensation-legacy-check.log`。
- Android AAR 重新构建并通过锁文件验证，SHA256 `a07aec9196901308b809f0823961146dd65c49680e00ae8b25fa616348f45e86`。
  来源为 Android 本地工作区，锁文件记录状态和 Java 源码哈希；生成 AAR 继续被 Git 忽略，不跟踪二进制。
- Unity 2022.3.14f1 APK 构建成功，v2 签名通过，已安装至 `10.168.2.104:5555`。
  制品：`build/samples/android/spatialxr-xri-cpu-sdk-compensation.apk`，56,033,385 字节；
  SHA256 `1d7665528f86f652cf14ac057b197087d19d22613992a7a204228412e96da847`。
  构建日志：`build/logs/xri-cpu-compensation-build.log`。
- 实机新进程默认 CPU，广播 CPU → GPU → CPU 均成功；创建、推理和资源关闭留在 `SpatialCamera-UnityHands` 消费线程。旧 `hands` 场景及返回 `xri` 正常启动。
  切换日志：`build/logs/cpu-gpu-sdk-compensation-switch.log`；最终 APK 冒烟：`build/logs/cpu-sdk-compensation-final-device.log`。
  无手观测 AI 约 17～24FPS / UVC 30FPS / Render 60FPS，不代表有手时的 CPU 性能或稳定性能验收。恢复 CPU 的 `xri` 场景供用户复测。

本次自动检查使用合成手部数据；实机检查期间为 hands=0。迁移后的侧掌、左右手、双手、捏合和镜片可读性仍需佩戴复测，不能用本次日志替代人工验收。

# 小米 UVC 与手势识别进展及接续计划

更新日期：2026-10-09。适用设备：小米 2206123SC（mayfly），外接 HXQ UVC 摄像头
VID `11cb` / PID `281b`。本文记录当前工作区与实机结果；较早的 RhinoX 验证记录不能直接替代本设备验收。

## 当前结论

- UVC 采集与 Unity 预览已通，当前请求 640×480、30 FPS，同尺寸优先 MJPEG。
- 自动曝光下曾随画面变化在约 20～30 FPS 间变化；当前先使用手动曝光 16.6 ms。
- 手动曝光 16.6 ms 和 33.3 ms 时用户实测可达到 30 FPS；66.6 ms 时约 15 FPS。
  结果支持曝光时间限制实际出帧率，但尚未确认自动模式的内部曝光行为及读回值为何不变。
- 手势识别已使用 MediaPipe Gesture Recognizer、`VIDEO` 模式、GPU delegate，最多两只手。
  用户最近有手日志约 22～24 FPS，推理约 35～43 ms，转换约 2.4～3.4 ms；尚未达到稳定 30 FPS。
- 已有场景切换、手势开关和曝光设置广播接口，可在同一场景内调整，不必重新加载场景。
- 小米物理光学、尺度和空间手势投影仍未验收，见
  [光学与尺度计划](XIAOMI_OPTICS_SCALE_VALIDATION.md)。当前优先推进图像手势识别性能与质量。

## 本次完成的工作与证据

### UVC 帧率与曝光排查

最初主要观察到 `captureFps≈20`、`previewFps≈20`，Unity 渲染约 60 FPS。
关闭手势识别后仍随画面变化出现 20/30 FPS，说明手势推理不是该变化的必要条件。
手掌进入画面与帧率变化的关联不能当成“识别结果影响相机”的证据。

加入约每 2 秒一次的曝光诊断，读取曝光模式、绝对曝光时间和曝光优先级，不自动修改参数。
实际自动模式记录为 `8(APERTURE_PRIORITY)`，曝光时间一直报告 16.6 ms，
即使采集从约 30 降到 20；曝光优先级显示 `unsupported`。
画面亮度变化也可能涉及增益，不能仅凭截图断定实际曝光值。

加入手动设置及读回日志，实机确认：

| 设置/查询 | 结果 |
| --- | --- |
| 手动模式 1，16.6 ms | 读回 `actualMode=1`、`actualTimeUs=16600`、`timeMatched=true` |
| 手动模式 1，10 ms | 读回 10000 us，采集可达到约 30 FPS |
| 恢复模式 8 | 读回 `8(APERTURE_PRIORITY)` |
| 曝光时间范围 | 原始 `[3, 2047, 166]`，即最小 0.3 ms、最大 204.7 ms、默认 16.6 ms |
| 低于时间范围的请求、无效模式 | 拒绝并记录日志，不作为成功设置 |
| 模式范围查询 | 返回 `[0, 0, 8]`，不足以可靠枚举支持的模式；实际以设置后读回为依据 |

用户进一步对照：16.6/33.3 ms 可达 30 FPS，66.6 ms 约 15 FPS。
曝光时间倒数是曝光造成的帧率上限之一，最终还受请求帧率与设备处理限制；
这不是所有光照、连接和长时间运行条件下的稳定性保证。

当前标准 UVC 控制没有“自动曝光最大时间”接口，该设备也未声明支持曝光优先级控制。
暂时采用手动曝光；以后再调查独立增益控制或厂商私有扩展，不假设已经支持。

### 手势识别优化

| 阶段 | 当前设备观察 | 结论与边界 |
| --- | --- | --- |
| IMAGE + 默认 CPU | 一只手约 10 FPS，推理约 108 ms，转换约 2.6 ms | 主要耗时在识别调用 |
| VIDEO + CPU | 有手约 8～10 FPS，用户采样推理约 87～128 ms | 单独切 VIDEO 未显示明显改善 |
| VIDEO + GPU 初测 | 无手约 21～29 FPS；少量单手记录约 17 FPS、56～59 ms | GPU 初始化及推理已通过 |
| VIDEO + GPU 后续用户采样 | 一/两只手约 22～24 FPS、35～43 ms | 相比此前有手约提升 2～3 倍；不同采样条件，非严格基准测试 |

`VIDEO` 在跟踪有效时可减少重复手掌检测。输入使用帧时间戳的毫秒值，
并保证同一模型实例内严格递增，手势开关不重置视频时间戳。

GPU 模型在 `SpatialCamera-UnityHands` 消费线程创建、调用和释放；
CameraService 的订阅关闭接口支持同线程清理。已验证离开相机场景再返回时 GPU 正常重建。
GPU 初始化失败会明确记录并回退 CPU；识别运行失败仍保留 CPU Hand Landmarker 回退路径，
因此必须检查 `mode` 和 `delegate`，不能只看界面上有手。

历史 2026-09-30 RhinoX VIDEO 分类曾发生图内部错误并改用 IMAGE。
本次小米 GPU/VIDEO 短时验证未观察到该错误，不代表长期回归及 RhinoX 兼容性已验收。

## 继续测试的命令

包名 `com.spatialai.samples.xr`。示例广播只有应用运行并完成初始化后才有效，
不负责冷启动；`am broadcast` 完成不是参数已经设置或场景已经加载的确认。
WiFi ADB 当前地址 `10.168.2.114:5555`；多设备时在每条 `adb` 后加 `-s 10.168.2.114:5555`。

```powershell
adb shell am start -W -n com.spatialai.samples.xr/com.unity3d.player.UnityPlayerActivity
# 等待初始化，随后开启手势并固定曝光 16.6 ms
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene hands --ez gestures true --ei exposureMode 1 --ei exposureTimeUs 16600
# 关闭推理，保留 UVC 采集和预览
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene hands --ez gestures false
# 恢复原来的自动曝光模式，保持手势关闭
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene hands --ez gestures false --ei exposureMode 8
# 返回 IMU / 光学场景
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene imu
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene optics
# 实时日志
adb logcat | Select-String 'SpatialUVC|SpatialHands|SpatialAI PHONE'
```

`gestures` 省略时默认开启。`exposureMode` 使用 1/2/4/8，`exposureTimeUs` 为正微秒值且必须是 100 的倍数；
设置时间需要手动或快门优先模式。省略曝光参数保留当前设置；显式请求在本进程中保留，
相机重新打开时重试应用，不持久保存到下次进程启动。读回不匹配、设备不支持或范围非法都不能当作成功。

## 日志解释与测量边界

| 字段 | 含义 |
| --- | --- |
| `SpatialUVC captureFps` | Java NV21 回调频率，约 2 秒窗口；已包含原生解码/处理影响，并非 USB 原始帧数 |
| `exposureMode` / `exposureTimeRaw` / `exposureTimeMs` | 设备读回模式、原始 0.1 ms 单位及毫秒值 |
| `exposurePriority` | 0 固定帧率、1 允许 AE 调整帧率；该设备报告 unsupported |
| `exposure set ... timeMatched` | 请求与读回对照，不能证明固件报告等于真实物理曝光 |
| `SpatialAI PHONE fps` | Unity 场景渲染帧率，通常约 60，与相机/识别帧率不同 |
| `previewFps` | Unity 纹理更新频率，约 1 秒窗口；窗口差异可导致略高于 captureFps |
| `inference` | 累计推理计数，日志每 30 次输出一次 |
| `mode=gesture` / `runningMode=VIDEO` / `delegate=GPU` | 完整分类路径、连续视频模式、请求启用的 GPU delegate；部分内部处理仍可能由 CPU 执行 |
| `hands` | 本次检测的手数量，非某个具体手势类别的命中数 |
| `SpatialHands fps` | 根据结果完成间隔计算的平滑帧率 |
| `convertMs` | 输入图像转换等前置耗时；首次调用也可能包括模型初始化，不用首次帧当稳定样本 |
| `inferMs` | MPImage 构造、完整模型调用、结果提取及输入关闭的耗时；未拆分检测/关键点/分类 |
| `captureToResultMs=-1` | UVC 尚无可靠曝光时间戳，该指标不可用 |

预览与推理是独立订阅线程，只保留最新待处理帧。识别慢时跳过旧帧，不要求识别完全部 30 FPS 输入。
预览链路仍有缩小图像、JPEG 编码、JNI 数组传递和 Unity `Texture2D.LoadImage` 开销。

## 下一步执行顺序（尚未实现）

1. **增加稳定的性能统计。** 固定曝光 16.6 ms、位置、光照、双手上限和 GPU/VIDEO 配置，
   预热后分别测试无手、单手静止、单手移动、双手各至少 60 秒。
   保存完整日志，统计推理和结果间隔的平均值/P50/P95、丢帧数，并记录温度、运行时长和画面条件。
   不能将平滑 FPS 的均值直接当作真实吞吐；通过计数变化/实际时间计算。
2. **增加“仅关键点 / 完整手势”对照接口。** 沿用广播，在同样 GPU/VIDEO 配置下比较
   Hand Landmarker 与 Gesture Recognizer 的总体耗时。它能测出完整链路差异，
   不能直接当作分类器独立耗时。决定是否需要类别识别，还是关键点与捏合交互已满足产品需求。
3. **定位跟踪与模型阶段耗时。** 单手静止/移动对比后，如需区分手掌检测、关键点和分类，
   使用 MediaPipe profiling 或受控图级诊断。确认是否频繁重新检测，再调整阈值，
   同时验证漏检、误识别、遮挡和重新入镜恢复，避免仅为帧率降低准确性。
4. **减少非模型开销与抖动。** 记录缓冲池/订阅丢帧、预览编码和 Unity 上传耗时，
   再考虑复用像素/图像缓冲、减少 JSON 与分配、改进预览传输。
   当前转换仅约 3 ms，不能期待只优化转换就将 40 ms 推理稳定压进 33.3 ms。
5. **完成稳定性与交互验收。** 双手、类别置信度、捏合、离镜/遮挡恢复，
   手势开关、场景往返、后台恢复、USB 重连与长时间温升测试；检查 GPU 失败回退。
   小米 UVC 内参、手机/眼睛外参和空间深度尚未标定，不将图像识别通过视为空间交互通过。

目标若为 30 FPS，每次完整处理平均需落在约 33.3 ms 内，并留意慢帧及准确率。
当前目标与方案是后续优化方向，尚未承诺稳定 30 FPS。

## 代码、构建与工作区交接

Android 独立仓库 `D:/visor-x/spatial-ai-android`：

- `runtime/platform/android/hands/UvcCapture.java`：USB 模式选择、采集计数、曝光读写。
- `runtime/platform/android/hands/SpatialHandBridge.java`：GPU/VIDEO、手势开关、图像预览、模型回退。
- `runtime/platform/android/camera/CameraService.java`：帧缓冲、订阅丢帧与同线程模型清理。
- `tools/BuildHandDiagnostic.ps1`：生成诊断 APK 与 `build/artifacts/spatial-hand-unity.aar`。

SDK 入口：

- [SpatialHandTracking.cs](../packages/com.spatialai.sdk/Runtime/Unity/SpatialHandTracking.cs)：Unity JNI、曝光/手势请求、预览纹理。
- [场景广播 Java 插件](../samples/unity/SpatialXR/Assets/Plugins/Android/SpatialXRSceneControl.java) 与
  [Unity 场景控制器](../samples/unity/SpatialXR/Assets/SpatialXR/SpatialXRSceneControl.cs)。
- [示例 README](../samples/unity/SpatialXR/README.md)：完整接口与构建说明。
- [Android AAR 锁文件](../third_party/spatial-ai-android.lock.json)：最新制品 SHA-256 与源码状态。

Android 本轮源码已提交为 a7e8ddc3ac3456aa97f1fa421576a4354807e14e，SDK 锁文件已更新到该 revision。
手势 AAR 是生成制品，SDK 不跟踪它；保留 .aar.meta 与锁文件，首次构建按示例 README 从 Android 仓库构建并校验复制。
当前 AAR SHA-256 为 593257bbda4d9eb03998516bbb08917ce3ca08d776fe787ae03c279b0e60c6a9。
后续修改 Android 源码时，应先提交源码、构建制品，再更新 SDK 锁文件，避免来源与制品不一致。

本机最新 APK：`build/samples/android/spatialxr-hands-gpu.apk`，已验证签名、安装和启动。
使用 Unity `D:/unity/hub/editor/2022.3.14f1/Editor/Unity.exe`、SDK `D:/android_sdk`、NDK `23.1.7779620`。
本次实际构建项目位于忽略目录 `build/takeover2022/samples/unity/SpatialXR`，
通过 `SpatialXRSetup.BuildAndroidSample` 构建；后续修改 Assets 时需同步到该副本或使用原始工程构建。
Android 库需重建、复制到 SDK `packages/com.spatialai.sdk/Plugins/Android/` 并更新 SHA-256 后再打 Unity APK。
`tools/VerifyAndroidHandAar.ps1` 可核对已打包制品；可通过 -AndroidRepoPath 指定已检出锁定 revision 的 Android 仓库并恢复忽略的 AAR。

证据位于本机忽略的 `build/logs/`：`uvc-exposure-*`、`uvc-manual-*`、
`hands-video-*`、`hands-gpu-*`，包括构建和设备检查日志；不保证其他检出目录拥有这些文件。
用户后续单/双手 22～24 FPS 日志来自本次会话，表中已明确其来源。

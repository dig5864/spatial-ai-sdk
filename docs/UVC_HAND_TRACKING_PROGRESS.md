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

面向 Unity 标准手部接口的实现、交付与验收阶段见
[Unity XR Hands 分阶段接入计划](UNITY_XR_HANDS_IMPLEMENTATION_PLAN.md)。本节保留识别性能优化路径。

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

## 2026-10-10：XR Hands 阶段 2 补充

新增独立 HXQ 内参/外参配置、单目米制估算和 21→26 关节转换；所有空间输出明确为估算，
未测配置现在仅提示，缺少参数时补开发预览默认值，允许继续发布估算结果。
标定工具、JSON 加载和严格测量评估保留，精度验收暂缓。Android AAR 未变，GPU/VIDEO/手动曝光基线保留。
手机标定采集场景可通过示例广播 `scene=calibration` 进入、`scene=capture` 保存图像。
摄像头安装位置已固定，用户可采集；实际内参/外参及静态、动态对位仍待验收，
不能复用 RhinoX 候选参数。采集、拟合和误差报告步骤见 [阶段 2](UNITY_XR_HANDS_STAGE2.md)。
本次还没有将真实输入接入 XRHandSubsystem，这项工作在阶段 3。

同日示例补充：手机分支启用 `HandTrackingDemo`，UVC 分支使用阶段 2 转换器显示独立三维手骨架，
关闭手势场景的相机预览矩形；预览保留在标定采集场景。缺少标定时使用开发默认值并提示，
通过 `uvcCalibrationJson` TextAsset 仍可加载参数。绿色 Left、青色 Right；加载的 `swapHandedness` 同步修正骨架与文字。
这里只提供物理头部局部空间的随头预览，没有加入真实 XR Hands Provider 或世界空间手部跟踪。
采集 640×480 @30，Android 实际模型输入及预览均为全视野 384×288。
编辑器集成验证 104 项通过，覆盖双手骨架、输入顺序变化、加载配置/左右手修正、退化、超时隐藏和启停恢复：
`build/logs/uvc-3d-hand-demo-check.log`。

用户确认镜片中只有左右反向、上下正常：小米 `reflectHandDisplayX=true` 仅在手骨架/捏合指针展示时
取反 X，物理转换、手别、相机输入与头姿不变。当前光路与输出方向的组合决定补偿轴，
不是所有单次反射都应反 X。补偿集成验证 150 项通过：`build/logs/uvc-3d-hand-mirror-check.log`。

用户确认移动方向已正确，但真实左手的原始标签为 Right，真实右手为 Left。
小米/HXQ 无标定预览启用 `uvcSwapHandedness=true`，统一修正骨架颜色、文字、手势和捏合的解剖手别。
显示层 X 补偿继续保留，不再次镜像位置；原始帧不改写。已加载标定 JSON 时以 `swapHandedness` 为准，
包括 false；此安装的配置应填 true。阶段 3 接入默认参数时需继承 Host 的这一设置。
Unity 集成验证 161 项通过：`build/logs/uvc-3d-hand-handedness-check.log`。

翻手反馈之后，新增图像空间的连续身份关联与最多 150 ms 的显示退化保留；严格空间转换仍拒绝不可靠几何。
Android 增加独立左右手置信度，Unity 记录手别跳变、拒绝原因、恢复、超时与关键点分片快照。
UI 的 G/rawH 分别代表手势和原始手别分数，不能互用；LOCK 表示连续身份锁定覆盖了当前原始标签。
当前仍需抓实际侧掌数据决定后续深度方案。
新 Android AAR 为本地未提交源码构建，锁文件已记录本地状态及修改文件哈希，生成 AAR 继续忽略。
抓取命令、日志字段、行为限制和当前制品来源见 [翻手诊断文档](UVC_HAND_DIAGNOSTICS.md)。

### 同日补充：依据翻手日志改善侧掌和漏检显示

实机日志 uvc-hand-turn.log 显示 Palm axes disagree、原始手别跳变和 not detected；因此增加展示退化。
同一身份侧掌造成掌宽/掌长深度冲突时，沿用最近有效相机距离，并投影当前 21 点，显示 depth-held。
距离仅在正常几何恢复时更新；不能跨身份继承，关键点不完整/越界或其他拒绝原因不进入该路径。
短暂漏检保留最近画面最多 120 ms，显示 missing-held，按渲染帧到期隐藏。
严格空间转换仍是 Untracked；退化画面不悬停或选择目标。先以正常掌姿获得距离，再翻手验收。
快照分片改为 600 字符，避免实机 1400 字符分片被截断。日志增加左右相机距离与漏检保留到期事件。
Unity 集成检查 188 项通过：build/logs/uvc-side-palm-check-final.log，包含持续侧掌、关键点跟随、
身份重建禁止复用旧距离、漏检到期无新帧也隐藏及全部快照分片重建 JSON。
操作与限制见 [翻手诊断文档](UVC_HAND_DIAGNOSTICS.md)。

### Unity XR Hands 阶段 3 实机复测（2026-10-10）

真实 UVC 输入已接入标准 XR Hands。用户确认左右手骨架、移动方向、双手显示和手移出/返回恢复正常。
USB 拔插后重新获取并人工确认权限，可以恢复识别。
首次打开应用和 USB 重插后的权限确认流程记录为 **UVC-PERM-001**，暂缓处理；
后续需区分 USB 设备授权与 Android 运行时权限，排查请求/回调/重连流程并改善重复请求及拒绝后重试。
详细记录与后续验收见 [阶段 3 文档](UNITY_XR_HANDS_STAGE3.md)。

### Unity XR Hands 阶段 4（2026-10-10）

XRI 射线、捏合选择与 UI 点击已实现，39 项契约检查、54 项集成检查及 20 次设备启停通过。
APK 已安装，无摄像头场景显示、场景往返及前后台恢复通过。
用户暂不连接摄像头，真实交互保留待验收；操作和完整清单见 [阶段 4 文档](UNITY_XR_HANDS_STAGE4.md)。

### 3DOF UI 与标准手势调整（2026-10-12）

当前交互场景移除可见射线、白球、骨架与可选方块，仅用食指圆圈配合普通 Unity Button 捏合点击。
应用使用 Unity XR / XR Hands / XRI 标准接口；手势定义使用 XRHandShape/XRHandPose，事件使用官方 Gestures 样例 StaticHandGesture。没有新增 Spatial 专有手势事件。
自动验证通过 46 项契约与 142 项集成检查，含双手 UI、丢失恢复、官方手势事件和生命周期。实机佩戴验收继续保留，操作见 [阶段 4 文档](UNITY_XR_HANDS_STAGE4.md)。
### 2026-10-12：保留骨架与方块，圆圈不受捏合弯曲影响

恢复标准 XR Hands 骨架和两个 XRSimpleInteractable 方块，保留世界固定位置与普通 UI。圆圈使用食指根部加掌部横轴 15mm 偏移，与骨架采用相同显示倍率和反射补偿；不读取弯曲指尖来定位，不在捏合时冻结位置，整手移动实时更新。
调试文字顶边下移至约 0.95 单位，字体 30，添加深色背景；OPEN/FIST/PINCH/NONE 合并在左右手文字状态中，不显示按钮式手势块。
应用事件继续使用 Unity XR Hands 官方 StaticHandGesture / XRHandTrackingEvents，方块使用 XRSimpleInteractable，按钮使用 Button.onClick；不新增应用手势接口。
全新 Unity 工程验证日志：`build/validation/xr-hands-stage4-2e8da17e8f9d443e84fb14bd033b2287/verification.log`。检查覆盖圈选方块、松手释放、捏合前后位置不变、捏合期间整手移动、骨架恢复、手势文字与无按钮式图形、下移背景及字体大小、双手 UI 和丢失恢复。
APK：`build/samples/android/spatialxr-xri-hand-circle-cubes.apk`；SHA256 `40f229edd0416f949dc71cfb2a0fd7a05b2b44dd31dc72adc6c96242cceefa7d`，v2 签名通过，已安装至 10.168.2.104:5555。
实机日志及截图：`build/logs/xri-hand-circle-cubes-device.log`、`build/logs/xri-hand-circle-cubes-device.png`。启动确认骨架/方块开启、visibleRays=false、cursorPinchHold=false，UVC 约 30 FPS，GPU VIDEO 识别正常启动，无运行时异常。截图确认骨架、圆圈、两方块、下移文字 UI，且方块悬停正常；镜片合相、圆圈相对食指位置、捏合稳定性和手势准确性仍需人工验收。
### 2026-10-12：按视频调整圆圈位置、目标跟随视线、中央调试面板

依据 `C:/Users/Administrator/456.mp4`，圆圈改用拇指尖与食指尖中点的视线投影，固定深度 0.72 × 展示倍率（默认 2.16 单位）；仅改位置，原有圆环网格、尺寸、材质与颜色逻辑保留。保留骨架和标准 XRI 交互，不新增应用手势接口。
方块与普通 UI 改为 XR 相机局部坐标，随头部转动保持视野位置。调试面板位于方块下方、按钮上方，顶边高 -0.12、底边 -0.46、深度 2.8；深色背景和文字比方块近表面 3.975 更近，避免方块遮挡，不拦截输入。
验证日志：`build/validation/xr-hands-stage4-2e8da17e8f9d443e84fb14bd033b2287/verification-thumb-index-follow.log`。通过 46 项契约、171 项集成检查、20 次生命周期循环、7 次模拟 UI 点击；覆盖圆环尺寸保留、中点镜面投影与深度变化、方块/按钮随相机转动、调试背景前后关系、捏合和丢失恢复。
APK：`build/samples/android/spatialxr-xri-thumb-index-follow.apk`；SHA256 `a105c32e41651669e53aa21960c199bcb82516c95dcf5d11147407bef7ce3709`，v2 签名通过，已安装至 10.168.2.104:5555 并切换到 xri 场景。
实机日志与截图：`build/logs/xri-thumb-index-follow-device.log`、`build/logs/xri-thumb-index-follow-device.png`。日志确认 thumbIndexCursor、headLocked、centralBackground，UVC 约 30 FPS、手动曝光 16.6ms、GPU VIDEO 推理启动。截图确认调试面板在中部偏下，与方块和按钮错开；当前真实输入 hands=0，圆圈相对手部位置、捏合体验及镜片可读性仍待佩戴人工验收。

### 2026-10-12：默认 CPU、SDK 统一补偿与三项 FPS

Android MediaPipe 默认改为 CPU / VIDEO；SDK 配置项和 Demo 原广播的 `inferenceDelegate` 支持 CPU/GPU 切换。
应用手部与交互仍使用 Unity XR / XR Hands / XRI 标准接口，没有新增应用手势事件。
身份关联、侧掌最近距离投影、短暂骨架保留和漏检隐藏迁入基础 SDK，由旧 hands 场景和标准 XR Hands 可视化共用，旧场景不再独立补偿。
显示缓存不作为有效跟踪数据，不触发悬停、点击或手势事件。相机、后端或识别状态失效时清空缓存。
三个场景调试 UI 增加 AI / UVC / Render FPS 与实际 CPU/GPU；无手结果仍显示推理帧率，过期后清零。
xri 面板增加一行，底边 -0.54、按钮高 -1.15，维持中央背景与目标错开。圆圈位置和样式、骨架与方块保留。

阶段 3 109 项、阶段 4 47 项契约与 172 项集成、旧场景 191 项检查通过；标准生命周期和 UI 回归通过。
实机 CPU → GPU → CPU 切换、旧场景往返通过；最终 APK 已安装至 10.168.2.104，恢复 CPU xri。
当前实际输入 hands=0，迁移后的翻掌/双手/捏合效果和镜片显示仍需佩戴复测。
完整命令、策略、限制、制品哈希与验证日志见 [SDK 补偿文档](UVC_HAND_SDK_COMPENSATION.md)。

### 2026-10-12：SDK 消费工程交付

用户已确认此前 CPU 默认、侧掌/手别补偿和基础交互无问题。SDK 四个版本化包、安装/升级说明与独立示例已完成本地预览交付；隔离工程导入、标准接口模拟运行、升级保留和 Android 构建通过。用户新工程已配置交互包和两个场景，并通过模拟运行检查。
新 APK 的手机安装被设备限制拦截，真实消费工程 UVC/3DOF 验收保留；不将模拟结果当作真实识别性能验收。USB 权限与小米光学/标定待办不变。详细结果见 [阶段 5](UNITY_XR_HANDS_STAGE5.md)。

### 2026-10-12：本轮接入完成与文件整理

后续用户确认消费工程 APK 手势场景正常，Android XR 配置后显示正常，并确认基础 SDK tgz 自动安装三个配套包验证没有问题。本轮 Unity XR Hands/XRI 接入及预览 SDK 安装交付完成，详见 [阶段 5](UNITY_XR_HANDS_STAGE5.md)。
SDK/Android 源码、示例、文档、标定工具和回归验证工具保留并暂存；用户确认后，21 个一次性迁移/用户 Demo 操作脚本和旧安装记录已删除。清单见 [改动与暂存清单](UNITY_XR_HANDS_CHANGE_REVIEW.md)。USB 授权、光学与米制精度、长时间性能及跨机型验证继续保留。

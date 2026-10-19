# Spatial XR Unity 示例工程

当前小米 UVC 曝光排查、VIDEO/GPU 手势优化结果与后续执行计划见
[UVC 与手势识别进展](../../../docs/UVC_HAND_TRACKING_PROGRESS.md)。

这是 Unity **2022.3.14f1c1** 消费工程，通过相对路径引用本仓库的 Spatial AI UPM 包和 Cardboard XR Plugin v1.30.1。Unity 6 与团结引擎仍需分别导入和实机验证。工程内三个场景用于不同测试，默认 APK 包含三个场景并从 `ImuDiagnostics` 启动。

## 场景与用途

当前 Demo 在 `Assets/Resources/SpatialAI/ScreenRig.json` 内置了已在 mayfly / dada 上核对的固定支架毫米配置，各台手机按 Android 各轴 DPI 或本机保存的裸屏测量尺寸计算双目区域。APK 包名沿用 `com.spatialai.samples.xr`，默认启动 IMU 场景。另一种支架应替换该资源；完整说明见 [多手机屏幕适配](../../../docs/PHONE_SCREEN_ADAPTATION.md)。

安装 Demo 后，原独立测试应用 `com.spatialai.validation.screen` 的测量文件不会跨包名自动共享。首次在每台手机运行时可点顶部 **Screen size / 50 mm ruler**，核对两条标尺后 **Save measured screen**，再关闭恢复显示；只更新本机尺寸，不重新建立支架。未保存时使用 API 估算，两台已测试手机的估算标尺均已由用户核对。

IMU 场景显示 Yaw / Pitch / Roll、原始 Cardboard 与映射姿态、手机陀螺仪和加速度。安装姿势固定后轻触屏幕归零，分别左右转、抬头低头、左右倾斜，检查视野跟随方向与轴向。新机型使用支架内的候选 IMU 映射，仍标记 MODEL UNVERIFIED；验证结果不由屏幕适配测试自动推定。

| 场景 | 用途 | 构建入口与输出 |
| --- | --- | --- |
| `Assets/Scenes/HelloSpatialXR.unity` | Cardboard 双目、3DoF 头姿、注视方块、RGB/MediaPipe 手骨架与捏合计数 | 双击 `BuildAndroid.bat` 或菜单 `Spatial AI > Build Android XR Sample`；`build/samples/android/spatialxr.apk` |
| `Assets/Scenes/ImuDiagnostics.unity` | 头姿轴向与归零、手机传感器读数、空间尺寸目标 | 菜单 `Spatial AI > Build IMU Diagnostics APK`；`build/samples/android/imu-diagnostics.apk` |
| `Assets/Scenes/OpticsComparison.unity` | 2160×2160 网格图的双目畸变对比 | 菜单 `Spatial AI > Build Optics Comparison APK`；`build/samples/android/optics-comparison.apk` |

三个 APK 都是诊断或预览应用。构建输出在 SDK 根目录的 `build/`，不上传 Git。

## 首次打开与构建

1. 首次打开 Unity 前，在 SDK 根目录运行 `powershell -NoProfile -ExecutionPolicy Bypass -File tools/BuildRhinoXCardboard.ps1 -NdkPath "<Android NDK 目录>"`，准备固定版本 Cardboard 插件及 RhinoX 诊断 AAR。需要 Git、Android NDK、CMake、Ninja 和 Python；首次获取上游依赖需要网络。生成 AAR 位于 `build/native/cardboard-rhinox-v1301/`，不入 Git；详情见 `third_party/rhinox/README.md`。
2. 在 Unity Hub 打开本目录，等待 Package Manager 导入。工程内已保存 XR Loader 和三个场景；普通开发者无需重新生成场景。`XR Camera` 使用 Input System `Tracked Pose Driver` 读取头部旋转，Cardboard XR Provider 负责双目渲染。
3. 在 Windows 上双击 `BuildAndroid.bat` 构建默认 APK。它检测工程所需 Unity 版本、Android SDK Platform 34 和 Build Tools 30.0.3，并在打开 Unity 前增量准备 Cardboard 原生依赖。如需指定 SDK，设置 `SPATIALAI_ANDROID_SDK_ROOT`；指定 NDK 使用 `SPATIALAI_ANDROID_NDK_ROOT`，否则优先查找 Unity 自带 NDK。仅检查环境可运行 `BuildAndroid.bat -CheckOnly`，不会编译或获取依赖。脚本会复用已打开的 Unity 编辑器，或启动它后自动构建。日志在 `build/logs/`。
4. 连接测试设备后运行 `adb install -r build/samples/android/spatialxr.apk`，再启动应用。首次启动手部预览需允许相机权限。构建脚本本身不安装 APK。

Unity Android 构建回调会在菜单、自动构建和 batchmode 打包前运行同一原生准备脚本，检查补丁并更新插件 AAR；依赖准备失败则停止构建。Unity 内使用 `SPATIALAI_ANDROID_NDK_ROOT` 或 External Tools 中配置的 NDK。原生源码无变化时增量构建复用结果，相同 AAR 不重写。Windows PowerShell 是当前构建流程的要求。

## 通过 ADB 切换场景

摄像头来源参数为 `--es cameraSource uvc/rear/front`：外接 UVC / 手机后置 / 手机前置，Demo 新进程默认 `uvc`；省略保留当前设置。
SDK 可使用 `SpatialHandTracking.CameraSource = SpatialHandCameraSource.Rear`（或 `Front` / `Uvc`）。SDK 默认 `HostProfile` 保留原有设备档案行为，Demo 显式设置 `Uvc`。
运行中切换来源或尺寸档位会重开采集并清除旧结果。CPU/GPU、最大手数、VIDEO 模式、输入宽度及限频保持原设置；UVC 曝光配置保留，但不应用到内置 Camera2。
内置摄像头由独立 `BuiltinCapture` 选择对应朝向的彩色 YUV Camera2 设备，复用 `CameraService` 采集；不存在对应摄像头时明确报错，不替换成另一朝向。
内置的三档从实际 `YUV_420_888` 尺寸中选择并按像素数排列，尽量接近 640×360、640×480、1280×720；档位 2 优先 VGA，同时保留低、高档。
可用模式少于三种时只提供实际模式数量，越界档位报错，不虚构或重复分辨率。每次打开都会打印实际档位尺寸及 FPS 范围，优先请求设备支持的最窄包含 30 FPS 的范围；不支持时保留设备默认 FPS。
内置图像按传感器和屏幕方向旋转为正向输入，不进行前置镜像；帧的 `imageWidth/imageHeight` 是原始采集尺寸，`inputWidth/inputHeight` 是实际 MediaPipe 输入尺寸。

```sh
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene xri --es cameraSource rear --ei cameraSizeTier 2
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene xri --es cameraSource front --ei cameraSizeTier 1
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene xri --es cameraSource uvc --ei cameraSizeTier 2
adb logcat -s SpatialBuiltin:I SpatialUVC:I SpatialHands:I
```

内置摄像头支持图像关键点与原始手势结果；XRI HUD 以 `RAW` 显示原始手势，以 `PREVIEW` 标明默认骨架投影。
XRI Demo 自动启用 `SpatialUvcHands.builtinSkeletonPreview`，使用 30° 水平视角、中心主点、零畸变、相机与头坐标重合和固定 0.5 m 深度绘制 26 个关节点。
这些是临时显示假设，全部标定验证标记保持未验证；关键点按 MediaPipe 实际旋转后的输入尺寸投影，不套用 UVC 的反转左右手或安装参数。
骨架仅绘制，不产生已跟踪的 XR 手或 UI/方块点击；手消失或图像超过 350 ms 未更新后隐藏。切回 UVC 时恢复原有转换和标定。
SDK 的该预览开关默认关闭；需要临时显示时可在启用组件前设为 true。需要对齐或空间交互时仍须增加对应摄像头标定。
日志 `pipeline source` 区分来源；Camera2 的 `capturedTotal/captureFps` 统计实际取出的图像（`acquireLatestImage` 可能跳过未取出的旧图像），其他送入、完成、跳过计数和耗时与 UVC 一致。

原始 UVC 采集尺寸参数为 `--ei cameraSizeTier N`：`1=640×360`、`2=640×480`（默认）、`3=1280×720`，按像素数量从小到大排列。
这三个档位是固定的原始采集尺寸，不是对 640×480 图像做软件缩放；仅在摄像头确实报告支持时启用，优先选择支持 30 FPS 的模式，再优先 MJPEG。
其他摄像头不支持所选尺寸时会明确报告 `Unsupported cameraSizeTier`，不会静默选择其他尺寸。省略参数保留当前设置，新进程恢复默认档位 2。
SDK 对应配置为 `SpatialHandTracking.CameraSizeTier`，运行中切换会重新打开 UVC、清除旧的手部数据；USB 授权仍需由设备允许。
MediaPipe 输入宽度保持 384，高度按采集宽高比计算：档位 1/3 为 384×216，档位 2 为 384×288。
XRI HUD 第一行的 `Capture` 是实际采集尺寸，`Input` 是送入 MediaPipe 的图像尺寸；`Input 384×216` 不表示摄像头只采集了 384×216。
RhinoX 向前拍摄的 RGB Camera2 摄像头（本次设备 ID 2）在 Android 中标记为 Front，可通过 `cameraSource front` 验证三档；支持上述尺寸时选择 640×360、640×480、1280×720。通用 Camera2 路径根据设备支持的模式选择三个递增档位，不保证其他设备也具有这三个尺寸。
`HostProfile` 的 `rhinox-rgb` 专用路径仍使用固定的标定采集尺寸（优先 1440×1080），不应用 `cameraSizeTier`，以免未经标定的尺寸变更影响图像到空间射线的转换。
改变采集宽高比后需核对该模式的视野及相机标定，不能把帧率测试视为交互精度验证。

```sh
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene xri --ei cameraSizeTier 1
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene xri --ei cameraSizeTier 2
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene xri --ei cameraSizeTier 3
adb logcat -s SpatialUVC:I SpatialHands:I
```

`SpatialUVC` 启动日志逐项列出摄像头支持的原始尺寸、格式与 FPS，`Selected` 显示实际选择的档位及尺寸。
`SpatialHands pipeline` 每约两秒输出真实计数窗口（含没有检测到手的推理帧）：

| 字段 | 含义 |
| --- | --- |
| `capture` / `input` | 原始采集尺寸 / 实际构造的 MediaPipe 输入尺寸 |
| `capturedTotal` / `callbackTotal` | 本次采集会话累计 UVC 回调数量 / 推理消费线程收到的帧数 |
| `submittedTotal` / `completedTotal` | 累计调用模型的次数 / 模型成功返回的次数 |
| `captureFps` / `callbackFps` / `submittedFps` / `completedFps` | 按窗口数量除以实际秒数计算的帧率，不使用平滑估算 |
| `disabledSkipped` / `throttleSkipped` | 本窗口因识别关闭或停止 / 33 ms 限频跳过的数量 |
| `queueReplaced` / `poolDropped` | 本窗口推理待处理帧被新帧替换 / 帧缓冲池不足丢帧的数量 |
| `errors` | 本窗口模型初始化、推理或结果处理错误数量 |
| `convertMeanMs` / `convertMaxMs` | 成功返回帧的缩放与 Bitmap 转换平均 / 最大耗时，不含模型初始化 |
| `inferMeanMs` / `inferMaxMs` | 成功返回帧的 MPImage 构造、模型调用及输入关闭平均 / 最大耗时 |
| `receiptToResultMeanMs` | UVC 主机收到帧到模型返回的平均延迟，包含排队；不是摄像头曝光到显示延迟 |

重开采集后累计计数归零；窗口按消费线程回调结束统计，首次窗口可能包含模型初始化时间。

手势识别使用 MediaPipe `VIDEO` 模式，默认 CPU 推理、固定单手；有效跟踪时可复用连续帧结果，
减少重复手掌检测。`SpatialHands` 日志包含 `runningMode=VIDEO` 和 `delegate=GPU/CPU`。
GPU 模型在相机消费线程中创建、调用和释放。GPU 初始化失败时明确记录错误并回退 CPU；
以日志中的实际 delegate 为准。
广播新增 `--es inferenceDelegate GPU` 或 `CPU`，可在 `hands`、`xrhands`、`xri` 中切换；省略保留当前设置，新进程默认 CPU。
通过 `--ei maxHands 1` 设置单手，`--ei maxHands 2` 设置双手，新进程默认固定单手。
省略参数保留本进程当前设置；已在 XRI 场景时重复发送广播也会立即应用，无需重新加载场景。
单手表示最多同时识别一只手，可以是左手或右手；双手表示最多两只手。
该参数直接配置 MediaPipe 推理数量，手势识别器及手关键点回退模型都使用此数量。
非法值会拒绝整条请求。SDK 代码也可使用 `SpatialHandTracking.MaxHands = 1` 或 `2`。

```sh
# 单手 XRI
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene xri --ei maxHands 1
# 双手 XRI（运行中切换）
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene xri --ei maxHands 2
```

`SpatialHands` 的 `Recognizer ready` 和推理日志包含实际 `maxHands`；切换时旧手部数据会失效，等待新模型结果。
三个场景显示 AI/UVC/Render FPS，旧场景和 XR Hands 场景已共用 SDK 补偿，详见 [SDK 补偿与推理配置](../../../docs/UVC_HAND_SDK_COMPENSATION.md)。
输入使用帧时间戳并保证毫秒值严格递增；手势开关不会重置同一模型实例的时间戳。

可在相同广播中设置曝光：`--ei exposureMode 1 --ei exposureTimeUs 16600` 表示手动曝光 16.6 ms，
`--ei exposureMode 8` 恢复当前 HXQ 摄像头原来的自动曝光模式（光圈优先）。
模式使用 UVC 原始值 1/2/4/8；时间单位是微秒，必须为正且是 100 的倍数。
设备曝光时间范围在设置前检查，模式设置后读回确认；`SpatialUVC` 中的 `exposure set` 输出请求值和实际值，
`timeMatched=false` 或 `rejected/failed` 不能视为设置成功。设备报告值是否代表实际曝光仍需画面对照。
省略两个曝光参数时保留当前设置；显式曝光请求在本进程内保留并在摄像头重新打开时应用。
手势参数仍默认开启，单独测试曝光时应继续携带 `--ez gestures false`。

```bash
# 手动曝光 16.6 ms，关闭手势
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene hands --ez gestures false --ei exposureMode 1 --ei exposureTimeUs 16600
# 恢复光圈优先自动曝光，保持手势关闭
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene hands --ez gestures false --ei exposureMode 8
```

UVC 曝光诊断：每个约 2 秒的采集帧率窗口后，`SpatialUVC` 输出一行 `exposure` 日志，
读取 `exposureMode`、`exposureTimeRaw`、`exposureTimeMs` 和 `exposurePriority`。
模式值：1=手动、2=自动、4=快门优先、8=光圈优先；曝光原始单位为 0.1 ms。
优先级：0=固定帧率、1=允许自动曝光调整帧率。不支持的参数显示 `unsupported`，读取异常显示 `read-error`。
参数是日志读取时刻的设备报告值，`captureFps` 是前一个窗口的平均值，二者并非逐帧曝光关联。
诊断只读取参数，不修改曝光设置。

```powershell
adb logcat -s SpatialUVC:I
```

`hands` 场景默认开启手势识别。添加 `--ez gestures false` 可关闭识别，保留 UVC 采集和预览；
即使当前已经在该场景也会立即生效，无需重新打开摄像头。恢复时使用 `--ez gestures true`，或省略该参数。
开关在本次进程内保持，场景 HUD 显示 `Gestures ON/OFF`，`SpatialAI PHONE` 日志记录 `gestures=True/False`。

```bash
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene hands --ez gestures false
```

默认 APK 包含三个场景，可用 Android 广播切换，无需重新安装。先启动应用，等待 Unity 初始化完成：

```powershell
adb shell am start -W -n com.spatialai.samples.xr/com.unity3d.player.UnityPlayerActivity
```

然后发送场景切换广播：

```powershell
# UVC 画面与手势预览
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene hands
# IMU 与尺度测试
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene imu
# 光学对比图
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene optics
```

支持短名 `hands`、`imu`、`optics`，以及完整场景名 `HelloSpatialXR`、`ImuDiagnostics`、`OpticsComparison`。其他场景名会拒绝；重复请求当前场景不会重新加载。加载期间收到新请求时，完成当前加载后处理最后一个有效请求。

接口是此示例的开发调试功能，通过动态 BroadcastReceiver 注册，仅在应用进程运行并完成初始化后有效，不负责冷启动。日志 `SpatialXRScene: Broadcast ready` 表示接收器就绪；Unity 日志 `Spatial XR scene active: ...` 表示切换完成。`am broadcast` 返回完成不等于 Unity 场景已经加载完成。单场景诊断 APK 不能切到未打包的场景。

```powershell
adb logcat -s SpatialXRScene:I Unity:I
```

切换离开相机场景会销毁对应组件并关闭相机消费者；切回时重新初始化。UVC 预览仍需连接摄像头，并允许相机及 USB 权限。

实现使用 Unity 的 [Java 源码插件](https://docs.unity3d.com/2022.3/Documentation/Manual/AndroidJavaSourcePlugins.html) 和 Android 的 [动态广播接收器](https://developer.android.com/develop/background-work/background-tasks/broadcasts)，没有替换 Unity 主 Activity。

若修改过 XR 场景，可使用 `Spatial AI > Repair XR Sample Camera` 修复 `Tracked Pose Driver` 并保存。`Spatial AI > Create Cardboard XR Sample Scene`、`Create IMU Diagnostics Scene`、`Create Optics Comparison Scene` 用于重新生成场景，会覆盖当前打开的场景；先保存自己的改动。

## 默认 XR 与手势预览

画面应显示双目视图和状态文字。正前方青色方块被注视时变黄；持续注视约 1.5 秒或使用 Cardboard Viewer 扳机后变绿，`Hits` 增加。它是注视交互示例，**独立 3DoF 手柄尚未接入**。

RhinoX 开发设备使用历史 `Sharp_2k_Seperate` OST 网格。状态文字中的 `RhinoX Pro Sharp mesh: ON` 表示原生渲染器已启用该网格，仍需在眼镜中检查双眼对齐、畸变方向与边缘。没有 Viewer 参数时，示例可能载入 Cardboard 通用测试参数并显示 `GENERIC TEST`；它不代表 RhinoX 光学校准完成。

同一场景会从 RGB Camera2 读取帧并显示最多两只手的 21 点骨架、捏合计数、实际推理帧率和耗时。图像点经过 RhinoX RGB 内参和畸变校正，再按开发设备的候选 RGB→eye 外参投到眼空间。`Depth approx` 使用手掌图像尺寸估算深度；手的实际尺寸和朝向会影响该值。它还不是精确的手部位置，也未接入 Unity XR Hands。Android 手势预览最低需要 API 28，iOS 手势尚未实现。

### 手势交互验收

默认 XR 场景在视野下方生成两个青色球。`HandTrackingDemo` 订阅 SDK 的 `SpatialHandTracking.FrameUpdated` 事件，并将拇指与食指指尖投到球体所在平面；小圆点是指向位置。依次检查：

1. 伸手后骨架和小圆点出现，`Events` 持续增加；移走手约 0.35 秒后小圆点消失。
2. 将小圆点移动到青色球上，球变黄；**保持指向并捏合一次**，球变绿，`Select` 增加一次。松开再捏合应再增加一次。
3. 分别用左右手测试；`Pinch` 是所有检测到的捏合起始次数，`Select` 只统计命中球体的次数。若 `Pinch` 增长而 `Select` 不变，先检查小圆点是否真正进入黄色区域。
4. 状态文字的 `Left` 与 `Right` 分别显示 MediaPipe 手势类别及 0–1 置信度。依次测试张掌、握拳、食指向上、拇指向上／下、剪刀手和“我爱你”；类别未命中时显示 `None`。两只手同时入镜时分别观察两行是否更新。`PINCH` 是 SDK 根据关键点计算的独立状态，不属于内置分类。

球体固定在头部前方，用于验证 SDK 事件、RGB→eye 投影和捏合触发链路；它不验证世界锚点、精确手深度或标准 `XRHandSubsystem`/XRI 输入绑定。

Android 相机和 MediaPipe 库的源码、诊断工程在独立的 [`spatial-ai-android`](http://gitlab.ximmerse.com/visor-x/spatial-ai-android) 仓库。手势 AAR 不进入 Git；首次构建先在 Android 仓库生成，再用 SDK 的 `tools/VerifyAndroidHandAar.ps1 -AndroidRepoPath <路径>` 校验并复制到 Unity 包。Unity Gradle 后处理器固定 MediaPipe Tasks Vision `1.0.0`。

## IMU 与空间尺寸场景

`ImuDiagnostics` 显示 Cardboard XR 相对 yaw、pitch、roll、眼中心位置以及手机陀螺仪和加速度计数据。红 X、绿 Y、蓝 Z 轴在启动时置于视线前方，随后保持世界方向。点按屏幕、按触发键或发送 R 键可归零；不便触屏时使用 `adb shell input keyevent 46`。

场景在 **0.6 m** 放置 **20 cm** 方块，在 **1.0、1.3、1.6 m** 放置 **40 cm** 方块；**2.6 m** 处有 **4×2 m** 棋盘格，每格 **20 cm**。这些目标用于观察尺度与转头时的畸变。它们不在默认的 `HelloSpatialXR` 场景中。

## 光学场景与 iOS

`OpticsComparison` 使用与历史工程 `OpticsReference` 相同的 2160×2160 测试图。需分别截取两个 Runtime 的真机原始画面，比较每眼方向、中心网格和边缘；当前尚未完成光学等效验收。

菜单 `Spatial AI > Build iOS Xcode Sample` 可在安装 iOS Build Support 的 macOS Unity 中导出 Xcode 工程，再由 Xcode 签名安装。iPhone 与外接 RhinoX 的显示路由、通信和头姿尚未实机验证。更多已验证范围见 [验证记录](../../../docs/VALIDATION.md)。
# Xiaomi 2206123SC 手机开发配置（2026-10-08）

启动时使用 Android Build 的 Manufacturer=Xiaomi、Model=2206123SC、Device=mayfly 精确匹配
`packages/com.spatialai.sdk/Runtime/Unity/Resources/SpatialAI/HostProfiles/xiaomi-mayfly.json`。
ADB 序列号只用于选择安装目标，不用于 SDK 机型匹配。

- 显示：手机自身屏幕，横屏 LandscapeLeft，原生竖屏尺寸 1080×2400，目标 60 FPS。
  Cardboard 根据实际显示区域生成双目视图；无已保存 viewer 参数时使用通用测试参数。
  正式佩戴时仍需对应镜片的 QR 参数，此配置不代表手机搭配任意镜片的光学标定。
- 跟踪：Cardboard 的手机传感器姿态融合；不加载 RhinoX 的 IMU 安装变换和反畸变网格。
- 相机：只选外接 UVC，通过 UVCAndroid 1.0.13 请求 USB 权限并协商设备声明的格式。
  优先接近 640×480 的模式，同尺寸优先 MJPEG；设备声明支持 30 FPS 时选用 30 FPS。
  无外接摄像头时显示等待状态，不回退到手机内置摄像头；多 UVC 设备同时连接时拒绝自动选择。
  USB 回调转换为 I420 后进入 CameraService 共用缓冲池和订阅分发。
  时间戳目前是主机接收时刻，不是传感器曝光时间，不能据此声称完成 IMU/相机同步。
- 手机 Demo 显示帧率、姿态四元数和角度、相机状态、实际采集/预览帧率及独立订阅的诊断图像预览。
  预览帧率独立于采集和算法结果帧率。UVC 内参及其到手机/眼睛的安装关系
  尚未标定，因此不复用 RhinoX 的手部空间投影，也不声称已完成 UVC 空间手势交互。

当前为开发预览。Android AAR 对应源码提交和制品 SHA-256 已锁定到 third_party/spatial-ai-android.lock.json，可运行 tools/VerifyAndroidHandAar.ps1 核对。

Unity 2022 / AGP 7.1.2 的旧 D8 在处理 UVCAndroid 1.0.13 时崩溃，构建回调按
[R8 官方方法](https://r8.googlesource.com/r8/+/refs/heads/8.3#Replacing-R8-in-Android-Gradle-plugin)
固定使用 D8/R8 8.3.37。也可设置 `SPATIALAI_R8_JAR` 使用已下载的官方同版本 JAR；
本机位于 `build/toolchain/r8-8.3.37.jar`，已与 Google Maven 发布的 SHA-1 校验一致。

手机测试：长按屏幕左下角（左侧 20%、底部 20% 区域）超过 1 秒，在相机 Demo 与 OpticsComparison 对比图之间切换。源图为 Assets/OpticsComparison/Pattern_2160.png，通过每眼 OnRenderImage 输入 XR 反畸变管线。当前尺度测试、相机主场景与 OpticsComparison 场景显式启用 RhinoX 参考网格，通过原生 XR 层保留其 UV 方向和网格形状，映射到当前手机面板，仅用于参考输出核对，不表示手机镜片已校准。手机 IMU 不使用 RhinoX 外参。必须获取实际镜片参数后才能验收光学校正，不能将手机截图缩放后与 RhinoX 截图一致作为通过标准。

## 按手机型号配置 IMU

配置位置：packages/com.spatialai.sdk/Runtime/Unity/Resources/SpatialAI/HostProfiles/*.json。
按 manufacturer + model + deviceCode 精确匹配。当前启动默认进入 ImuDiagnostics 尺度测试。

imu 配置字段：
- calibrationVersion：可追溯版本；validatedForHardware：是否已实测确认。
- sourceFrame 固定为 cardboard-unity；输入是 Cardboard 已转换为 Unity 坐标的姿态，不是 Android 原始传感器四元数。
- screenOrientation 当前支持 landscape-left。安装方式改变后须重新校准；不能仅凭手机型号覆盖不同安装方向。
- basisX/Y/Z/W：单位四元数 B，表示坐标基变换，处理轴交换与符号；不是直接交换欧拉角。
- mountX/Y/Z/W：单位四元数 M，表示转换后坐标系中的头部/眼睛安装旋转。
- 输出公式 q = B * qInput * inverse(B) * M；invertInputRotation=true 时 qInput=inverse(qCardboardUnity)，否则 qInput=qCardboardUnity。
- preserveNeckModel：默认 false，3DoF 输出位置为零；true 保留 Cardboard 估算的颈部模型位置并按 B 转换，不代表真实位置追踪。

配置在原生 XR Input Provider 发布姿态前生效，Unity 标准 XR CenterEye、TrackedPoseDriver 读取同一输出；不在每个 Camera 上重复叠加。RhinoX 设备仍走原有 IMU-to-eye 外参分支。光学显示的 180 度旋转与 IMU 配置独立。

例如 B=(0,0,0.70710678,0.70710678) 将正 X 旋转映射为正 Y；B=(0,0,1,0) 同时反转 X/Y 的旋转方向而保留 Z。这些是数学示例，不是小米实测值。拒绝非单位四元数、NaN、错误源坐标系和不支持的屏幕方向。

小米当前候选配置 v4：B=identity，M=(0.38268343,0,0,0.92387953)，invertInputRotation=false，仍为 UNVERIFIED。2026-10-09 用户确认手机横放、屏幕朝外并向下倾斜约 45°，经一次反射后进入人眼。v2/v3 的轴置换及绝对姿态取逆未通过实机验证，已取消；Cardboard 已负责传感器到横屏 Unity 姿态的转换。M 是右乘的 +45° X 安装补偿候选，假设正常佩戴时原始 Unity 手机姿态约为 -45° pitch；实际装配角、横放左右方向和光路仍须逐轴及组合动作确认。反射属于显示光路，不通过对头部四元数取逆模拟；现有光学网格与输出方向保持不变。

验收：每组动作前点按归零，抬头时 Pitch 应减少，右转时 Yaw 应增加，右侧倾时 Roll 应减少；再右转 30° 后抬头，检查串轴。画面运动须以经镜片反射后的人眼观感为准。诊断日志同时输出 Raw CB q、Mapped XR q 和 Camera q，便于区分传感器映射、安装外参与显示光路问题。数学回归覆盖固定安装下的三轴及组合旋转，但不替代实机校准。

## 反射显示的俯仰响应候选（v5）

2026-10-09，v4 实测右转、右侧倾及右转后抬头已不混轴，但抬头时反射画面向上移动。保留 v4 的固定手机安装外参，增加独立的 `HostDeviceProfile.invertViewPitch=true` 开关。它在安装转换后调整虚拟视线的俯仰，保留视线的水平朝向及残余侧倾；不是把手机传感器四元数取逆，也不是新的物理 IMU 标定。默认关闭，仅小米开发配置开启。原生 XR Provider 统一发布调整后的虚拟视图姿态，Camera 与诊断读数读取同一结果。

此候选依据当前镜片中的动作反馈构建，不是任意单次反射光路的通用模型，不能替代已知镜面法线、投影及装配外参的光学校准。开关启用时 XR 姿态表示经过显示响应补偿的视图姿态，不应直接用作机器人/空间定位等用途的物理头姿。镜片中抬头画面应向下，右转仍向左，右倾表现应与 v4 一致；须复测组合动作后才能验收。

## 反射显示的水平线补偿候选（v6，当前）

用户明确要求内容对齐现实水平线：右侧倾头时，画面应相对视野向左补偿。此前把镜片中向右倾斜作为正确表现的验收结论取消。保留固定安装外参，当前小米配置同时开启 `invertViewPitch` 和 `invertViewRoll`。

两项同时开启时采用四元数变换 `qView=(-qHead.x, qHead.y, -qHead.z, qHead.w)`，对应垂直反射下旋转轴的转换。Yaw 保持，Pitch 和 Roll 反向；该变换保留旋转的乘法组合关系，不经欧拉角拆解。这里补偿的是视图姿态，原始 IMU 与固定安装映射不变，也不把 Roll 锁为零。单开俯仰时仍支持 v5 的行为。

实机验收须正常佩戴后归零：抬头背景向下，右转背景向左，右倾时背景相对视野向左倾并与现实水平线保持一致；再测试右转后抬头、右转后侧倾和往返动作。数学检查覆盖非零 Yaw/Pitch/Roll、四种开关组合以及连续旋转组合关系。此配置仍标记 UNVERIFIED，等待镜片中的实测确认。

## UVC 棋盘格采集（2026-10-10）

手机 `hands` 场景现在启用三维手骨架：使用 `SpatialUvcHandsConverter` 将 UVC 归一化关键点
转换成物理头部局部空间的米制估算点，显示为独立 3D 骨架；不叠加到相机图像。
手势场景不显示 UVC 预览矩形，原始预览仍可在以下采集场景查看。
采集为 640×480 @30，Android 模型输入和 Unity 预览均缩小为全视野 384×288。
默认无标定也显示，HUD 明示 `3D ESTIMATED` 与默认/未确认参数状态。
可给 `HelloSpatialXR.uvcCalibrationJson` 赋值 TextAsset，优先使用实测配置；不能复用 RhinoX 内参。
绿色骨架表示 Left、青色表示 Right；默认使用原始标签，标定中的 `swapHandedness` 会同步修正骨架和手势文字。
实际左右对应仍需人工确认。
当前小米/HXQ 无标定预览已根据用户实测启用 Host 的 `uvcSwapHandedness=true`：
原始 Right → 真实 Left（绿色），原始 Left → 真实 Right（青色），调试文字和捏合状态同步对应。
这是相机输入的手别映射，与显示层 X 反射独立；不重新反转位置或镜像输入图像。
赋值 `uvcCalibrationJson` 后以该 JSON 的 `swapHandedness` 为准（包括 false）；此安装的配置应填 true。
直接使用 HandTrackingDemo 且不加载 JSON 时，可设置组件的 `defaultUvcSwapHandedness`。
手离开/结果超过 0.35 秒后骨架隐藏，调试面板显示手势类别和捏合计数。
这是随头部的预览，不是房间尺度追踪；标准 `XRHandSubsystem` 消费使用阶段 3 新增的 `xrhands` 场景，见
[阶段 3 文档](../../../docs/UNITY_XR_HANDS_STAGE3.md)。旧 `hands` 场景保留独立预览和显示退化路径。

食指圆圈/UI 交互使用 `--es scene xri`（或 `interaction`），通过可选 XR Interaction 包构建进 APK。
保留手骨架与两个方块；圆圈参照 `456.mp4` 放在拇指/食指中点的投影位置，原有圆环样式不变。先张开手，将圆圈对准方块捏合选中、松开释放，或对准按钮捏合松开增加点击计数。方块和按钮跟随 XR 相机；调试面板位于视野中部偏下，带深色背景且比方块更近。无可见射线/白球，捏合不锁定圆圈位置。
调试文字下移并显示 OPEN/FIST/PINCH/NONE，不绘制按钮式手势块。
应用手势使用 Unity XR Hands 官方 Gestures 样例组件及 XRHandShape/XRHandPose。构建前先导入官方样例并等待编译，再生成交互场景；接入不新增 Spatial 手势事件接口。
配置、动作资产、prefab 和失效行为见 [阶段 4 文档](../../../docs/UNITY_XR_HANDS_STAGE4.md)。
日志 `SpatialHands3D:` 的 left/right 表示经过几何校验后的骨架是否有效；`SpatialHands` 的 hands 表示检测数量。
用户通过镜片确认手部显示只有左右反向、上下正常；小米 Host 配置启用 `reflectHandDisplayX=true`。
示例在最终显示骨架/捏合指针时执行 `(x,y,z) → (-x,y,z)`，只补偿当前镜片与输出方向的组合效果。
这不修改 `inputMirrored`、`swapHandedness`、SDK 物理关节或 IMU；不能通过旋转四元数表示反射。
其他 Host 默认关闭；更换镜片/安装/输出方向后重新确认，不能从单次反射数量推断该轴。
直接使用 HandTrackingDemo 的工程可在组件上设置 `reflectUvcDisplayX`；XR Hands 的物理数据仍保持反射前坐标。
编辑器集成验证：将 `tests/UnitySmoke/UvcHandDemoCheck.cs` 和 `tests/CoreChecks/UvcGeometryChecks.cs`
复制到已有 Cardboard 示例的 `Assets/Editor`，使用 `-executeMethod UvcHandDemoCheck.Run` 执行。

翻手诊断版增加手别连续关联、150 ms 的显示退化保留及问题关键点快照。
UI 用 G/rawH 分开显示手势分数/原始左右手置信度，标签锁定时显示 LOCK；`fresh`/`held`/`hidden` 表示空间显示状态。
抓取关键字 `SpatialHandsDiag`（logcat tag 为 Unity），操作与字段详见
[UVC 翻手诊断](../../../docs/UVC_HAND_DIAGNOSTICS.md)。

新构建包含 `SpatialUvcCalibration` 场景。运行 App 后用原广播接口的 `--es scene calibration`
进入采集场景，保持 `--ez gestures true --ei exposureMode 1 --ei exposureTimeUs 16600`。
点 Capture 或发 `--es scene capture` 保存预览图和同名相机元数据。
采集图保存在应用 `persistentDataPath/uvc-calibration/`；保持摄像头安装位置固定，采集 25～30 张多角度棋盘图。
当前保存的是 384×288 全视野 JPEG 预览解码图，不能当成原生 640×480 无损帧。
打印板、导出、拟合和精度验证见 [阶段 2 文档](../../../docs/UNITY_XR_HANDS_STAGE2.md)。

### 手势场景远距离显示（2026-10-10）

为方便合相调整，HandTrackingDemo 的 presentationDistanceScale 默认 3，可在 Inspector 启动前设置 1～10。
骨架、目标、指针、调试 UI 和背景统一放在相机下的展示根节点，以等比缩放放远，保持原有角大小和局部交互坐标。
目标从 0.8 m 显示到 2.4 m；UI 从 1.8 m 显示到 5.4 m；骨架显示距离为转换输出的 3 倍。
UI characterSize 从 0.006 改到 0.0075，额外放大 25%，背景同步扩大 25%。
这是显示距离调整，SDK 关节、标定与日志中的物理估计距离保持不变；不能据此宣称完成双眼光学校准。
## FP16 / INT8 模型广播对照

新进程默认使用官方 FP16。应用运行后可在 `hands`、`xrhands`、`xri` 场景通过 `modelVariant` 切换；省略参数保留当前选择，退出进程后恢复 FP16。

```sh
# 保持 CPU 固定单手，只切换模型进行对照
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene xri --es inferenceDelegate CPU --ei maxHands 1 --es modelVariant INT8
adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene xri --es inferenceDelegate CPU --ei maxHands 1 --es modelVariant FP16
```

切换会重新打开采集并重建识别器，清除旧结果、跟踪状态和统计窗口。HUD 显示 `Model FP16` / `Model INT8`，`SpatialHands` 的 `Recognizer ready` 日志包含 `modelVariant` 和 `modelAsset`，每帧结果也携带这两个字段。
SDK C# 配置为 `SpatialHandTracking.ModelVariant = SpatialHandModelVariant.FP16` / `INT8`。
INT8 是 dynamic_wi8_afp32 实验版，四个子模型来自独立 Android Demo 同一组量化文件，输入输出仍为 FP32，非全整数静态 INT8。
INT8 初始化/推理失败不会回退官方模型；GPU 初始化失败仍可用同一 INT8 模型回退 CPU，实际 delegate 以日志为准。
对比时保持相机、分辨率、手数、后端和动作一致，切换后等待首帧和跟踪稳定，再观察耗时、丢手、误识别和温升。

Android AAR 构建可使用 `BuildHandDiagnostic.ps1 -Int8GestureModelPath <gesture_recognizer_int8.task>` 加入校验后的实验文件；官方 FP16 文件名和下载校验保持不变。模型与 AAR 的 SHA-256 记录在 `third_party/spatial-ai-android.lock.json`。

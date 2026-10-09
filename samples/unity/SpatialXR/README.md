# Spatial XR Unity 示例工程

当前小米 UVC 曝光排查、VIDEO/GPU 手势优化结果与后续执行计划见
[UVC 与手势识别进展](../../../docs/UVC_HAND_TRACKING_PROGRESS.md)。

这是 Unity **2022.3.14f1c1** 消费工程，通过相对路径引用本仓库的 Spatial AI UPM 包和 Cardboard XR Plugin v1.30.1。Unity 6 与团结引擎仍需分别导入和实机验证。工程内三个场景用于不同测试，默认 APK 包含三个场景并从 `ImuDiagnostics` 启动。

## 场景与用途

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

手势识别使用 MediaPipe `VIDEO` 模式，优先 GPU 推理、最多两只手；有效跟踪时可复用连续帧结果，
减少重复手掌检测。`SpatialHands` 日志包含 `runningMode=VIDEO` 和 `delegate=GPU/CPU`。
GPU 模型在相机消费线程中创建、调用和释放。GPU 初始化失败时明确记录错误并回退 CPU；
以日志中的实际 delegate 为准。
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

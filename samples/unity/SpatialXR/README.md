# Spatial XR Unity 示例工程

这是 Unity **2022.3.14f1c1** 消费工程，通过相对路径引用本仓库的 Spatial AI UPM 包和 Cardboard XR Plugin v1.30.1。Unity 6 与团结引擎仍需分别导入和实机验证。工程内三个场景用于不同测试，默认 APK 只打包 `HelloSpatialXR`。

## 场景与用途

| 场景 | 用途 | 构建入口与输出 |
| --- | --- | --- |
| `Assets/Scenes/HelloSpatialXR.unity` | Cardboard 双目、3DoF 头姿、注视方块、RGB/MediaPipe 手骨架与捏合计数 | 双击 `BuildAndroid.bat` 或菜单 `Spatial AI > Build Android XR Sample`；`build/samples/android/spatialxr.apk` |
| `Assets/Scenes/ImuDiagnostics.unity` | 头姿轴向与归零、手机传感器读数、空间尺寸目标 | 菜单 `Spatial AI > Build IMU Diagnostics APK`；`build/samples/android/imu-diagnostics.apk` |
| `Assets/Scenes/OpticsComparison.unity` | 2160×2160 网格图的双目畸变对比 | 菜单 `Spatial AI > Build Optics Comparison APK`；`build/samples/android/optics-comparison.apk` |

三个 APK 都是诊断或预览应用。构建输出在 SDK 根目录的 `build/`，不上传 Git。

## 首次打开与构建

1. 在 SDK 根目录运行 `tools/BuildRhinoXCardboard.ps1`，准备固定版本 Cardboard 插件及 RhinoX 诊断 AAR。脚本需要 Android NDK、CMake、Ninja 和 Python；详情见 `third_party/rhinox/README.md`。
2. 在 Unity Hub 打开本目录，等待 Package Manager 导入。工程内已保存 XR Loader 和三个场景；普通开发者无需重新生成场景。`XR Camera` 使用 Input System `Tracked Pose Driver` 读取头部旋转，Cardboard XR Provider 负责双目渲染。
3. 在 Windows 上双击 `BuildAndroid.bat` 构建默认场景。它检测工程所需 Unity 版本、Android SDK Platform 34 和 Build Tools 30.0.3；如需指定 SDK，设置 `SPATIALAI_ANDROID_SDK_ROOT`。仅检查环境可运行 `BuildAndroid.bat -CheckOnly`。脚本会复用已打开的 Unity 编辑器，或启动它后自动构建。日志在 `build/logs/`。
4. 连接测试设备后运行 `adb install -r build/samples/android/spatialxr.apk`，再启动应用。首次启动手部预览需允许相机权限。构建脚本本身不安装 APK。

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

Android 相机和 MediaPipe 库的源码、诊断工程在独立的 [`spatial-ai-android`](http://gitlab.ximmerse.com/visor-x/spatial-ai-android) 仓库。当前 Unity 包自带已验证 SHA-256 的 AAR；更新后可用 SDK 的 `tools/VerifyAndroidHandAar.ps1 -AndroidRepoPath <路径>` 核对。Unity Gradle 后处理器固定 MediaPipe Tasks Vision `1.0.0`。

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

小米当前候选配置 v2：B=(0.5,-0.5,0.5,0.5)，M=identity，invertInputRotation=true，仍为 UNVERIFIED。依据用户三轴观察构造，归零附近对应 X←Y、Y←Z、Z←−X；三轴观察不足以证明完整姿态方向约定，必须继续验证非零朝向下的组合动作，不能将该参数泛化到所有小米或所有安装方式。Demo 的 Raw CB XYZ 是配置前的 Cardboard 姿态，Pitch/Yaw/Roll 是 XR 配置后姿态；点击归零时分别记录两组参考。测试时分别抬头、右转、右倾约 15 度，每组动作前归零，记录主变化轴与正负号后才能填写校准配置。没有实测证据时不自动根据显示旋转猜测 IMU 轴向。
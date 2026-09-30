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

球体固定在头部前方，用于验证 SDK 事件、RGB→eye 投影和捏合触发链路；它不验证世界锚点、精确手深度或标准 `XRHandSubsystem`/XRI 输入绑定。

Android 相机和 MediaPipe 库的源码、诊断工程在独立的 [`spatial-ai-android`](http://gitlab.ximmerse.com/visor-x/spatial-ai-android) 仓库。当前 Unity 包自带已验证 SHA-256 的 AAR；更新后可用 SDK 的 `tools/VerifyAndroidHandAar.ps1 -AndroidRepoPath <路径>` 核对。Unity Gradle 后处理器固定 MediaPipe Tasks Vision `1.0.0`。

## IMU 与空间尺寸场景

`ImuDiagnostics` 显示 Cardboard XR 相对 yaw、pitch、roll、眼中心位置以及手机陀螺仪和加速度计数据。红 X、绿 Y、蓝 Z 轴在启动时置于视线前方，随后保持世界方向。点按屏幕、按触发键或发送 R 键可归零；不便触屏时使用 `adb shell input keyevent 46`。

场景在 **0.6 m** 放置 **20 cm** 方块，在 **1.0、1.3、1.6 m** 放置 **40 cm** 方块；**2.6 m** 处有 **4×2 m** 棋盘格，每格 **20 cm**。这些目标用于观察尺度与转头时的畸变。它们不在默认的 `HelloSpatialXR` 场景中。

## 光学场景与 iOS

`OpticsComparison` 使用与历史工程 `OpticsReference` 相同的 2160×2160 测试图。需分别截取两个 Runtime 的真机原始画面，比较每眼方向、中心网格和边缘；当前尚未完成光学等效验收。

菜单 `Spatial AI > Build iOS Xcode Sample` 可在安装 iOS Build Support 的 macOS Unity 中导出 Xcode 工程，再由 Xcode 签名安装。iPhone 与外接 RhinoX 的显示路由、通信和头姿尚未实机验证。更多已验证范围见 [验证记录](../../../docs/VALIDATION.md)。

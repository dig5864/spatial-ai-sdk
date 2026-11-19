# 图像相对跟踪接入（实验阶段）

此功能仍在开发，尚未完成真机识别、坐标轴或双目贴合验证。使用自己的 Vuforia Native 11.4.4 Android 包与许可证；不要将许可证放入源码或日志。仅 Android ARM64；不使用 Vuforia Unity 插件。已构建独立测试 APK 并安装到 S23，未配置许可证及标定时显示等待配置。

## 构建

解压官方 ZIP，在 `SpatialAIAndroid/samples/android/hand-diagnostic` 的 Gradle 构建中传入 `-PvuforiaSdkDir=<解压目录>/build`，构建 `:hand-unity:assembleRelease`。可用 `-PvuforiaNdkVersion=<版本>` 或 `-PvuforiaNdkPath=<路径>` 配置 NDK。AAR 包含 Vuforia Java 库、arm64 原生引擎及自定义 Driver；相关依赖的使用和分发遵循 Vuforia 许可。测试 APK 已验证包含两个 Vuforia 原生库；SDK 正式发布包尚未自动携带此功能。

构建后将生成的 hand-unity AAR 作为现有手部 AAR 的替代品集成，并遵循既有 Unity SDK 的 Android 依赖配置。不得同时放入两份同名手势类或不同版本的 Vuforia 引擎。合并后的 Android Manifest 需要 CAMERA、INTERNET、ACCESS_NETWORK_STATE；运行时需授予相机权限和 UVC USB 权限。首次发起相机权限请求后再次调用启动接口。

## 调用与坐标

在 XR 头部下创建 `CameraMount`，创建目标锚点及其子物体，再向 `SpatialImageTracking` 设置 `CameraMount` 和 `TargetAnchor`。`StartTracking` 默认加载当前 UVC 标定并设置 `CameraMount` 的局部位置和旋转。组件会把锚点作为 `CameraMount` 的子对象，并仅在直接跟踪且结果未过期时显示；停止、暂停、丢锁或过期时隐藏。组件本身不能放在锚点或其子物体上。

在应用代码安全取得许可证和 Android 本地图像文件路径后调用：

```csharp
tracker.StartTracking(licenseKey, imagePath, printedWidthMeters,
    fps: 30, exposureMode: -1, exposureTimeUs: -1);
// 不再使用时
tracker.StopTracking();
```

例如，场景启动脚本可先创建锚点和物体，再将锚点赋给组件（`cameraMount` 已在 Inspector 中绑定到头部下的相机标定节点）：

```csharp
var anchor = new GameObject("ImageTargetAnchor").transform;
var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
cube.transform.SetParent(anchor, false);
cube.transform.localScale = Vector3.one * 0.05f;
cube.transform.localPosition = Vector3.zero;
tracker.CameraMount = cameraMount;
tracker.TargetAnchor = anchor;
tracker.StartTracking(licenseKey, imagePath, printedWidthMeters);
```

示例将方块中心放在目标锚点，不添加测试位置偏移。物体尺寸是展示选择；示例不负责准备密钥、图像或标定文件。

省略 `intrinsics` 时，`StartTracking` 根据标定的原始图像尺寸选择 UVC 模式。层级 2 是 640×480，1 是 640×360，3 是 1280×720。手势与图像跟踪同时使用时，尺寸和曝光配置必须一致。应用也可显式设置 `CameraMount`，并传入 12 项内参 `fx, fy, cx, cy, k1, k2, p1, p2, k3, k4, k5, k6`。虚拟物体相对于目标的偏移由锚点子对象局部变换控制。

状态可通过 `State`、`NativeStatus`、`Sequence` 读取，`3` 表示 Vuforia 直接跟踪。Native 输出列主序 OpenGL 目标相对相机矩阵，Unity 侧进行左右手坐标变换；真实轴向、目标法向和外参方向须通过正对、旋转及倾斜的实机检查确认。帧龄以主机收到 UVC 帧起算，并非曝光时间或端到端延迟。暂停后需要应用显式重新启动跟踪；避免在上一次异步停止完成前重启。

图像跟踪不提供离开视野后的房间级定位。进度和未完成的验收见 [实施记录](UVC_VUFORIA_IMAGE_TRACKING_PLAN.md)。

## 两种图像目标

SDK 内置清单位于 `Runtime/Unity/Resources/SpatialAI/VuforiaTargets/targets.json`。应用可调用 `SpatialVuforiaTargetCatalog.LoadBuiltIn()` 获取目标 ID、资源、印刷宽度档位和图像宽高比，再用每个目标的 `MaterializeImage()` 获取交给 Vuforia 的本地文件路径。`ResolveWidthMm()` 读取默认尺寸，也可校验应用选定的尺寸。更新 SDK 中的清单和原图后，消费工程重新构建 APK 即可使用更新后的目标。

独立测试应用同时创建蓝卡原图和交接包中的 `detail-85mm-control.png` 两个 Observer。后者文件名保留原始名称，但图案摘要与历史 140 mm 彩色细节图一致；当前两张实物图案的宽度均配置为 0.14 m。两者共享同一 UVC 帧与同一相机标定，不增加位置或旋转补偿。

SDK 的 `StartTracking` 末尾可选参数 `secondImagePath` 和 `secondWidthM` 用于加载第二张本地图像。设置 `SecondTargetAnchor` 后，两个锚点分别应用各自的直接跟踪位姿，独立处理丢失和过期。该模式需要支持双目标输出的 Android 桥接。单锚点仍显示最佳目标，`TargetId`、`TargetVersion` 和尺寸仍描述最佳目标。

## 当前无测试偏移版本

独立测试场景默认使用诊断应用共享的 UVC 标定，和手势追踪走同一个 SDK 标定入口。Vuforia 接收原始分辨率下的相机内参和畸变，Unity 将 `cameraToHead` 与可选的 `alignment.pose` 合成后应用到 `CameraMount`。目标锚点的方块局部位置为零，手部展示倍率为 1。

手部坐标旋转读取 `setup.cameraMountRotationDeg`。共享 JSON 缺少 `handTracking.handednessMapping` 时，沿用 SDK 的 UVC 左右手标签覆盖项或手机机型配置。手部画面水平反射沿用手机机型配置。

该版本移除了此次测试新增的固定平移、90° 常量、左右手和显示镜像常量；**不代表所有空间因素已经标定**。现有单目手部算法在掌部尺寸缺失时仍使用默认尺寸做深度估计，手部不是精确 6DOF；Unity 双目显示仍使用既有支架光学配置，没有应用本文件的 `alignment.eyes` 和 `alignment.optics`。这些限制必须在后续验证，不能据此承诺零误差或完整的标定驱动。

# Spatial AI SDK

本仓库是 Spatial AI SDK 主工程。`SDK/xms-xr-sdk` 是历史参考工程。Android CameraService 与手势库源码位于独立的 `visor-x/spatial-ai-android` 仓库；本仓库保留锁定版本的 Unity AAR。当前可运行的消费工程为 `samples/unity/SpatialXR/`；[Group 与仓库职责](docs/GROUP_OVERVIEW.md)、Unity 6、团结、WebXR、OpenXR 与 Native 的[目标目录和边界](docs/REPOSITORY_LAYOUT.md)另有说明。

## 当前状态

UPM 包版本为 `0.2.0-preview.8`。Unity 2022.3.14f1c1 示例已在 RhinoX 开发设备上运行 Android APK，包括 Cardboard 双目、RhinoX Pro 历史 OST 畸变网格、3DoF 头姿及 RGB Camera2 手部骨架。当前源码新增 MediaPipe 手势分类接口及左右手分类 Demo，真机分类准确率与性能待本版 APK 测试。手骨架根据相机内参与候选外参投到眼空间，深度由图像中的手掌尺寸近似估算。

当前光学与相机外参仍是开发设备的候选配置，手部深度不是精确测距。独立 3DoF 手柄、Unity XR Hands 接口、iOS 手势、Unity 6 与团结实机兼容、手机兼容列表及分体式手机与眼镜链路仍待实现或验证。`SpatialRuntimeHost` 的硬件 Backend 仍返回 Unsupported；Cardboard XR 示例使用独立桥接。最新边界见 [验证记录](docs/VALIDATION.md)。

## 目录

| 路径 | 用途 |
| --- | --- |
| `runtime/` | 核心 C ABI、会话、设备适配与标定参考 |
| `packages/com.spatialai.sdk/` | Unity/团结目标的 UPM 包源码 |
| `samples/unity/SpatialXR/` | Unity 2022 Android/iOS 消费工程与自动构建入口 |
| `samples/android/` | 独立 IMU 诊断工程；相机和手部诊断工程位于 Android 仓库 |
| `tests/` | Core、原生 ABI 与 Unity API 检查 |
| `tools/` | 内部构建、依赖获取和打包脚本 |
| `third_party/` | Cardboard 补丁及 Android AAR 的源码版本和校验值 |
| `docs/` | 架构、兼容性和验证记录 |
| `build/`、`dist/` | 本地构建中间文件与发布候选包，均不入 Git |

## 开始使用

示例入口与用途见 [SDK 示例索引](samples/README.md)。

1. 在新机器准备固定版本 Cardboard 依赖。运行 `tools/BuildRhinoXCardboard.ps1` 会获取 v1.30.1 源码和 Unity 插件，构建并安装 RhinoX 诊断 AAR；要求 Android NDK、CMake、Ninja、Python 和网络。具体要求见 [RhinoX 构建说明](third_party/rhinox/README.md)。
2. 在 Unity Hub 打开 `samples/unity/SpatialXR/`。工程通过相对路径引用本仓库 UPM 包和本地 Cardboard 插件。场景、XR Camera 以及 Android/iOS 设置见 [示例说明](samples/unity/SpatialXR/README.md)。
3. 在 Windows 上双击 `samples/unity/SpatialXR/BuildAndroid.bat` 构建 Android APK，输出到 `build/samples/android/spatialxr.apk`。该脚本不会自动安装；连接设备后用 `adb install -r build/samples/android/spatialxr.apk` 安装。手部预览最低需要 Android API 28。
4. 纯 Core 与原生 ABI 验证运行 `tools/Validate.ps1`；本地 UPM 发布候选包由 `tools/Pack.ps1` 输出到 `dist/<版本>/unity/`。候选包没有发布到远端。

本仓库与独立的 `spatial-ai-android` 项目位于 GitLab `visor-x` 分组。Unity 包中的 Android AAR 对应的源码提交与 SHA-256 记录在 `third_party/spatial-ai-android.lock.json`；`tools/VerifyAndroidHandAar.ps1` 可核对它。`build/`、`dist/`、上游检出和临时 Unity 工程留在本机。正式发布时将版本化的包和校验清单上传到制品仓库，参见 [目录设计](docs/REPOSITORY_LAYOUT.md)。

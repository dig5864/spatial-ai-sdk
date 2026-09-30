# Spatial AI SDK

本仓库是 Spatial AI SDK 主工程。`SDK/xms-xr-sdk` 是历史参考工程。当前可运行的消费工程为 `samples/unity/SpatialXR/`；Unity 6、团结、WebXR、OpenXR 与 Native 的目标目录和边界见 [目录设计](docs/REPOSITORY_LAYOUT.md)。

## 当前状态

UPM 包版本为 `0.2.0-preview.5`。Unity 2022.3.14f1c1 示例已在 RhinoX 开发设备上编译、安装并运行 Android APK，包括 Cardboard 双目、RhinoX Pro 历史 OST 畸变网格、3DoF 头姿、RGB Camera2 采集及 MediaPipe 手部骨架预览。手骨架根据相机内参与候选外参投到眼空间，深度由图像中的手掌尺寸近似估算；屏幕显示跟踪帧率、推理时间、画面年龄和估算深度。

当前光学与相机外参仍是开发设备的候选配置，手部深度不是精确测距。独立 3DoF 手柄、Unity XR Hands 接口、iOS 手势、Unity 6 与团结实机兼容、手机兼容列表及分体式手机与眼镜链路仍待实现或验证。`SpatialRuntimeHost` 的硬件 Backend 仍返回 Unsupported；Cardboard XR 示例使用独立桥接。最新边界见 [验证记录](docs/VALIDATION.md)。

## 目录

| 路径 | 用途 |
| --- | --- |
| `runtime/` | 核心 C ABI、会话、设备适配与标定参考 |
| `packages/com.spatialai.sdk/` | Unity/团结目标的 UPM 包源码 |
| `samples/unity/SpatialXR/` | Unity 2022 Android/iOS 消费工程与自动构建入口 |
| `samples/android/` | 独立相机、手部和 IMU 诊断工程 |
| `tests/` | Core、原生 ABI 与 Unity API 检查 |
| `tools/` | 内部构建、依赖获取和打包脚本 |
| `third_party/` | Cardboard 版本、补丁及许可证记录 |
| `docs/` | 架构、兼容性和验证记录 |
| `build/`、`dist/` | 本地构建中间文件与发布候选包，均不入 Git |

## 开始使用

1. 在新机器准备固定版本 Cardboard 依赖。运行 `tools/BuildRhinoXCardboard.ps1` 会获取 v1.30.1 源码和 Unity 插件，构建并安装 RhinoX 诊断 AAR；要求 Android NDK、CMake、Ninja、Python 和网络。具体要求见 [RhinoX 构建说明](third_party/rhinox/README.md)。
2. 在 Unity Hub 打开 `samples/unity/SpatialXR/`。工程通过相对路径引用本仓库 UPM 包和本地 Cardboard 插件。场景、XR Camera 以及 Android/iOS 设置见 [示例说明](samples/unity/SpatialXR/README.md)。
3. 在 Windows 上双击 `samples/unity/SpatialXR/BuildAndroid.bat` 构建 Android APK，输出到 `build/samples/android/spatialxr.apk`。该脚本不会自动安装；连接设备后用 `adb install -r build/samples/android/spatialxr.apk` 安装。手部预览最低需要 Android API 28。
4. 纯 Core 与原生 ABI 验证运行 `tools/Validate.ps1`；本地 UPM 发布候选包由 `tools/Pack.ps1` 输出到 `dist/<版本>/unity/`。候选包没有发布到远端。

本仓库计划作为一个私有 GitLab 项目放在 `visor-x` 分组下。Git 保存源码、Unity 场景与必要的包内二进制；`build/`、`dist/`、上游检出和临时 Unity 工程留在本机。正式发布时将版本化的包和校验清单上传到制品仓库，参见 [目录设计](docs/REPOSITORY_LAYOUT.md)。

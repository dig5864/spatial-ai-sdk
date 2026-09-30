# visor-x Group 说明

**Group ID：139**

**建议填写到 GitLab Group「Description」的简介：**

> Visor-X / Spatial AI SDK 研发空间。围绕手机计算的低成本 AR 应用平台，维护设备侧 Android 能力、Unity XR SDK、示例工程和技术文档；后续按产品进度扩展 Unity 6、团结、WebXR、OpenXR 与 Native。当前以 RhinoX Pro 开发设备验证，未完成的平台和硬件组合不视为正式兼容。

## 仓库分工

| 项目 | 职责 | 当前状态 |
| --- | --- | --- |
| [`spatial-ai-sdk`](http://gitlab.ximmerse.com/visor-x/spatial-ai-sdk) | 主 SDK：跨平台能力契约、Unity 包、Cardboard/RhinoX XR 适配、Unity 示例与文档 | Unity 2022 + RhinoX 开发预览；Unity 6、团结、iOS 实机及其他平台仍待验证 |
| [`spatial-ai-android`](http://gitlab.ximmerse.com/visor-x/spatial-ai-android) | Android 库：CameraService、MediaPipe 手部桥接、AAR 构建和独立诊断应用 | Camera2 RGB 与手势预览已在 RhinoX 开发设备验证；Vuforia、UVC 待接入 |
| [`xms-xr-sdk`](http://gitlab.ximmerse.com/visor-x/xms-xr-sdk) | 历史 XR 工程与光学、Unity XR 代码参考 | 历史参考；当前新功能和验收以 `spatial-ai-sdk` 为准 |
| [`demo`](http://gitlab.ximmerse.com/visor-x/demo) | Group 内已有 Demo 项目 | 本次整理不改变其内容与职责 |

Android 仓库产出的 Unity AAR 随 `spatial-ai-sdk` 包交付。SDK 仓库的 `third_party/spatial-ai-android.lock.json` 固定 Android 源码提交号和 AAR SHA-256，便于确认 Unity 使用的二进制对应哪份源码。

## `spatial-ai-sdk` 目录

| 目录 | 放什么 |
| --- | --- |
| `runtime/` | 平台无关的 Core、C ABI、会话和设备 Profile；不放 Unity 场景或 Android 手势 Java 源码 |
| `packages/com.spatialai.sdk/` | 对开发者交付的 Unity UPM 包：C# 接口、Cardboard 适配、设备参数、Android AAR、可导入 Sample |
| `samples/unity/SpatialXR/` | 可直接用 Unity 打开的消费工程；包含默认 XR/手势、IMU/尺度、光学对比三个场景 |
| `samples/android/imu-diagnostic/` | 独立 Android IMU 诊断应用，用于验证传感器到 Cardboard EKF |
| `tests/` | Core、原生 ABI 和 Unity API 检查 |
| `tools/` | 依赖获取、RhinoX Cardboard 构建、AAR 校验、Unity 包打包和验证脚本 |
| `third_party/` | Cardboard 版本锁、RhinoX 补丁与 Android AAR 来源锁定信息；上游完整源码检出不入 Git |
| `docs/` | 架构、兼容性、设备参数、设计与验证记录 |
| `build/` | 本机编译中间文件、APK、日志和临时工程；Git 忽略 |
| `dist/` | 本地版本化发布候选包；Git 忽略，正式交付时再上传制品库 |

Unity 示例的场景与构建步骤见 [`samples/unity/SpatialXR/README.md`](../samples/unity/SpatialXR/README.md)。

## `spatial-ai-android` 目录

| 目录 | 放什么 |
| --- | --- |
| `runtime/platform/android/camera/` | Camera2 相机发现、采集、帧生命周期与多消费者分发 |
| `runtime/platform/android/hands/` | MediaPipe 手部检测与供 Unity 读取的 Android 桥接 |
| `samples/android/camera-diagnostic/` | 相机选择、帧率及多消费者诊断 APK 的源码 |
| `samples/android/hand-diagnostic/` | 手势诊断 APK 与 Unity AAR 的 Gradle 工程 |
| `tools/` | 相机和手势诊断的构建脚本 |
| `build/` | 生成的 AAR、APK、模型缓存；Git 忽略 |

Android 示例入口见该仓库的 [`samples/README.md`](http://gitlab.ximmerse.com/visor-x/spatial-ai-android/-/blob/main/samples/README.md)。

## 开发与发布约定

1. 设备侧 Android 相机和手势能力先在 `spatial-ai-android` 实现与验证，再以固定提交和校验值更新 Unity 包内 AAR。
2. Unity/团结接口、XR 示例和跨平台能力契约维护在 `spatial-ai-sdk`。WebXR、OpenXR、Native 模块在需求和验收路径明确后再建立，不预先创建空工程。
3. Git 保存源码、场景、配置、许可证和必要的包内二进制；Unity `Library/`、Gradle 缓存、APK、构建日志与本地发布候选包不提交。
4. 当前目标是让外部开发者跑通稳定的 Unity XR 小应用。应用分发、账号、授权、支付和企业订阅属于后续平台阶段，尚非当前 SDK 已交付功能。

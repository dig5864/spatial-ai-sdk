# Spatial AI SDK

当前源码兼容 Unity 2022.3 / Unity 6：基础包 **0.2.0-preview.11**、XR Hands / XR Interaction **0.1.0-preview.3**，配套 Spatial 修改版 Cardboard。三个编辑器的测试与当前构建边界见 [兼容性验证](docs/UNITY_COMPATIBILITY.md)。用户安装与升级见 [安装说明](docs/SDK_INSTALL.md)，发布包与全新工程验收见 [阶段 5](docs/UNITY_XR_HANDS_STAGE5.md)。

## 2026-10-09 开发交接：小米手机 + UVC

本阶段为开发验证版本；当前启动默认进入 ImuDiagnostics 尺度测试。测试手机为 Xiaomi 2206123SC / mayfly，手机自身屏幕和 IMU，外接 SunplusIT HXQ UVC（11cb:281b）。

已完成：
- 按厂商、型号、设备代号匹配 HostProfiles；光学、IMU、相机来源分别配置。
- UVC 通过 Android 独立仓库接入 CameraService，手势推理与图像预览独立消费。测试中 Unity 渲染约 60 FPS，UVC 实际采集和预览约 20 FPS，手势推理约 18–22 FPS；格式协商为 640×480@30，不等于实测 30 FPS。
- 原生 XR 加载 RhinoX 原网格；手机使用用户提供的 AA 矩形：左 (268.220,157.045,865.054,865.054)，右 (1266.727,157.045,865.054,865.054)。坐标为 2400×1080 横屏、Android 左上角原点，不叠加刘海安全区偏移。
- 每眼等比例映射、几何裁切和 180° 输出方向配置；黄色框在 AA 外侧，间隔 2 像素、宽 3 像素。
- IMU 配置在原生 XR 输入层生效，标准 XR CenterEye 与 TrackedPoseDriver 共用。支持坐标基四元数、安装四元数、输入姿态取逆、可选颈部模型；RhinoX 设备外参独立。
- 尺度测试含绿色 20 cm Cube（中心距 0.6 m），青色/橙色/粉色 40 cm Cube（中心距 1.0/1.3/1.6 m），以及 20 cm 棋盘格。点按可重新摆放至当前朝向。

验证边界：
- 小米 IMU v2 是根据三轴观察构造的候选参数，仍为 UNVERIFIED。近单位姿态映射为 X←Y、Y←Z、Z←−X；取逆方案尚未完成实际佩戴和非零朝向组合动作验证。
- AA 像素边界已通过截图检查，但物理尺度、双眼融合、FOV、畸变及动态跟随尚需佩戴验收；不要把像素截图对齐当成光学校准通过。
- 51 项 Core 检查通过；Android AAR 哈希和原生补丁校验通过；Unity 2022.3.14f1c1 Android 构建、安装、启动及配置日志已验证。APK 位于 build/samples/android/spatialxr.apk，不进入源码 Git。

下一步（按顺序）：
1. 固定手机安装姿态，验证抬头/右转/右倾，以及右转 30° 后抬头；记录配置前后姿态，确认是否需要取逆或改为安装旋转。通过后才标记 IMU 校准完成。
2. 用尺子、实物和佩戴观察核对 20/40 cm 物体尺度、视距、IPD/FOV、左右眼方向及动态畸变。记录镜片型号和屏幕到镜片距离。
3. 排查 UVC 协商 30、实际约 20 FPS 的原因；测量曝光、传输、转换、推理、显示各段耗时。现有主机接收时间不代表曝光时间，端到端延迟尚未量准。
4. 优化取帧到 Unity 纹理路径（当前 JPEG 诊断通路）、双手推理和延时预测；完善 USB 拔插、权限、暂停恢复及多相机选择回归。
5. 扩展手机配置与兼容矩阵；逐步接入 Unity XR Hands、Vuforia 共享相机、Tag 定位。iOS、Unity 6/团结、WebXR/OpenXR/Native 按独立阶段验证。

同事上手：先按下文准备 Cardboard v1.30.1 构建依赖，再用 Unity 2022.3.14f1c1 打开 samples/unity/SpatialXR，运行 BuildAndroid.bat。小米参数见 packages/com.spatialai.sdk/Runtime/Unity/Resources/SpatialAI/HostProfiles/xiaomi-mayfly.json，IMU 字段定义和测试步骤见 samples/unity/SpatialXR/README.md。Android 源码和 AAR 对应关系以 third_party/spatial-ai-android.lock.json 为准。

本仓库是 Spatial AI SDK 主工程。`SDK/xms-xr-sdk` 是历史参考工程。Android CameraService 与手势库源码位于独立的 `visor-x/spatial-ai-android` 仓库；本仓库通过锁文件引用 Android 构建的 Unity AAR，制品不进入 Git。当前可运行的消费工程为 `samples/unity/SpatialXR/`；[Group 与仓库职责](docs/GROUP_OVERVIEW.md)、Unity 6、团结、WebXR、OpenXR 与 Native 的[目标目录和边界](docs/REPOSITORY_LAYOUT.md)另有说明。

## 当前状态

UPM 基础包版本为 `0.2.0-preview.11`。Unity 2022.3.14f1c1 示例已在 RhinoX 开发设备上运行 Android APK，包括 Cardboard 双目、RhinoX Pro 历史 OST 畸变网格、3DoF 头姿及 RGB Camera2 手部骨架。当前源码新增 MediaPipe 手势分类接口及左右手分类 Demo，真机分类准确率与性能待本版 APK 测试。手骨架根据相机内参与候选外参投到眼空间，深度由图像中的手掌尺寸近似估算。

当前光学与相机外参仍是开发设备的候选配置，手部深度不是精确测距。独立 3DoF 手柄、iOS 手势、Unity 6 与团结实机兼容、手机兼容列表及分体式手机与眼镜链路仍待实现或验证。`SpatialRuntimeHost` 的硬件 Backend 仍返回 Unsupported；Cardboard XR 示例使用独立桥接。最新边界见 [验证记录](docs/VALIDATION.md)。

## 目录

多手机屏幕参数读取、物理分屏与 50 mm 校正/支架配置复用见 [屏幕适配使用说明](docs/PHONE_SCREEN_ADAPTATION.md)。

Unity XR Hands 可选配套包已提供契约、模拟和真实 UVC Provider、标准关节场景与时序恢复。
模拟验证见 [阶段 1 文档](docs/UNITY_XR_HANDS_STAGE1.md)，真实输入与手机验收步骤见
[阶段 3 文档](docs/UNITY_XR_HANDS_STAGE3.md)。完整实机与空间精度验收仍待完成。

XRI 食指圆圈、方块捏合选择、UI 点击及标准 XR Hands 手势由独立 `com.spatialai.xr-interaction` 可选包提供，
包含动作资产、交互 prefab 和示例场景；安装与实机步骤见 [阶段 4 文档](docs/UNITY_XR_HANDS_STAGE4.md)。

阶段 2 已提供 HXQ 图像坐标到米制估算的转换、手机棋盘格采集与误差工具；实机标定仍待采集，
步骤见 [阶段 2 文档](docs/UNITY_XR_HANDS_STAGE2.md)。现有 APK 新增 `scene=calibration/capture` 的广播入口。
手部标定参数为可选项：缺少或未完成标定时提示并使用开发预览参数，精度验收后续补做。

| 路径 | 用途 |
| --- | --- |
| `runtime/` | 核心 C ABI、会话、设备适配与标定参考 |
| `packages/com.spatialai.sdk/` | Unity/团结目标的 UPM 包源码 |
| `packages/com.spatialai.xr-hands/` | 可选 XR Hands 配套包；模拟/真实 UVC Provider、时序恢复与标准关节场景 |
| `samples/unity/SpatialXR/` | Unity 2022 Android/iOS 消费工程与自动构建入口 |
| `samples/android/` | 独立 IMU 诊断工程；相机和手部诊断工程位于 Android 仓库 |
| `tests/` | Core、原生 ABI 与 Unity API 检查 |
| `tools/` | 内部构建、依赖获取和打包脚本 |
| `third_party/` | Cardboard 补丁及 Android AAR 的源码版本和校验值 |
| `docs/` | 架构、兼容性和验证记录 |
| `build/`、`dist/` | 本地构建中间文件与发布候选包，均不入 Git |

## 开始使用

示例入口与用途见 [SDK 示例索引](samples/README.md)。

1. 新机器首次打开 Unity 前，运行 `powershell -NoProfile -ExecutionPolicy Bypass -File tools/BuildRhinoXCardboard.ps1 -NdkPath "<Android NDK 目录>"`，获取固定版本 Cardboard 源码和插件并生成原生依赖。需要 Git、Android NDK、CMake、Ninja、Python，以及首次获取依赖时的网络连接。AAR 输出到已忽略的 `build/native/cardboard-rhinox-v1301/`，不入 Git；源码、补丁和构建脚本仍入库。具体要求见 [RhinoX 构建说明](third_party/rhinox/README.md)。
2. 在 Unity Hub 打开 `samples/unity/SpatialXR/`。工程通过相对路径引用本仓库 UPM 包和本地 Cardboard 插件。场景、XR Camera 以及 Android/iOS 设置见 [示例说明](samples/unity/SpatialXR/README.md)。
3. 在 Windows 上双击 `samples/unity/SpatialXR/BuildAndroid.bat` 构建 Android APK，输出到 `build/samples/android/spatialxr.apk`。脚本在启动或通知 Unity 前准备原生依赖，Unity 的 Android 构建回调也会执行增量准备，覆盖菜单和 batchmode 入口；原生补丁变化后会重新编译并更新插件 AAR，无变化时复用编译结果。可设置 `SPATIALAI_ANDROID_NDK_ROOT` 指定 NDK。该脚本不会自动安装；连接设备后用 `adb install -r build/samples/android/spatialxr.apk` 安装。手部预览最低需要 Android API 28。
4. 纯 Core 与原生 ABI 验证运行 `tools/Validate.ps1`；本地 UPM 发布候选包由 `tools/Pack.ps1` 输出到 `dist/<版本>/unity/`。候选包包含基础 SDK、XR Hands、XR Interaction 和修改版 Cardboard，以及 AAR/模型/源码校验清单；没有发布到远端。

本仓库与独立的 `spatial-ai-android` 项目位于 GitLab `visor-x` 分组。Unity 包中的 Android AAR 对应的源码提交与 SHA-256 记录在 `third_party/spatial-ai-android.lock.json`；`tools/VerifyAndroidHandAar.ps1` 可核对它。`build/`、`dist/`、上游检出和临时 Unity 工程留在本机。正式发布时将版本化的包和校验清单上传到制品仓库，参见 [目录设计](docs/REPOSITORY_LAYOUT.md)。

### 准备 Android 手势 AAR

新检出不包含手势 AAR。先检出锁文件 revision 对应的 spatial-ai-android 源码，在该仓库运行 tools/BuildHandDiagnostic.ps1 -AndroidPlayerPath '<Unity AndroidPlayer 目录>'，再在 SDK 仓库执行：

```powershell
.\tools\VerifyAndroidHandAar.ps1 -AndroidRepoPath '..\spatial-ai-android'
```

脚本检查源码提交及制品 SHA-256 后，将 build/artifacts/spatial-hand-unity.aar 复制到 Unity 包。该 AAR 已忽略，.aar.meta 和 lock.json 继续跟踪；Unity 构建不会自动重建手势库。重新构建的哈希若不匹配，应先核对源码和工具链，不直接绕过校验。


## 发布包与新工程验证

SDK 开发者在准备并校验 Android 手部与修改版 Cardboard 制品后，运行 `tools/Pack.ps1`（Python 3.9+）生成 `dist/<基础包版本>/unity/`。
打包生成四个功能包 tarball；基础包 `com.spatialai.sdk-<版本>.tgz` 内置安装器和三个配套包，用户在 Package Manager 中只需 Add package from tarball 导入基础包，即自动安装 Cardboard、XR Hands、XR Interaction。旧独立 installer 不再生成，升级时自动移除其引用。归档保留在用户工程内，安装状态支持程序集重载、重启及失败重试。AAR 不进入源码 Git，但进入发布包。已存在的同版本候选默认拒绝覆盖；迭代本地未发布候选时显式使用 `-ReplaceLocalCandidate`。
发布脚本检查模型、AAR、Android 源码文件、Cardboard 补丁和原生库的对应关系，输出确定性归档、源码校验值及本地修改快照。
用 `tools/ValidateXRHandsStage5.ps1 -UnityPath '<Unity.exe>'` 从发布 tarball 创建隔离工程，验证示例、纯显示模式、旧版序列化场景升级及两个 APK 构建。
用 `tools/ValidateUnityInstaller.ps1 -UnityPath '<Unity.exe>'` 验证仅导入入口 tarball 的自动安装、程序集重载、校验失败/重试及工程搬迁；该脚本是 SDK 开发验证工具，不是用户安装入口。
2026-10-12 用户确认基础 SDK 自动安装三包的验证正常。本轮实现、工具保留理由和待清理文件见 [XR Hands 改动与暂存清单](docs/UNITY_XR_HANDS_CHANGE_REVIEW.md)。
用户只需按安装说明导入发布包和配置 Android XR，不需要本仓库 tools 或独立 Android 源码仓库。

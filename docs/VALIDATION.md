# 验证记录

Unity 标准手部接口的后续实施与交付标准见
[XR Hands 分阶段接入计划](UNITY_XR_HANDS_IMPLEMENTATION_PLAN.md)。阶段 0 与阶段 1 模拟 Provider 已完成，真实 UVC 空间手部尚未接入。

## Unity XR Hands 阶段 1（2026-10-09）

- 配套包注册 `SpatialAI-Simulated-Hands` descriptor，新增 `SpatialHandSubsystem` / Provider、
  确定性左右手模拟源、所有者组件、标准 API 可视化、键盘/Inspector 控制及可导入场景 sample。
- Unity **2022.3.14f1** 隔离工程通过 **307 项**确定性检查：26 点布局/手别、米制姿态与半径、
  未支持速度、组合旋转与骨长、捏合收敛、缺失关节/旋转、失去跟踪、停止清理、恢复与归零版本。
- 图形模式进行了两次 Play/Stop，**各 20 次组件启停、10 次真实场景重载**，实例计数保持正确。
  8 帧观测窗口中每轮 Dynamic=8、渲染阶段=7，无重复回调；桌面无 XR Display 时渲染阶段由 LateUpdate 提供。
  真实 XR Display 的 `onBeforeRender` 硬件时序尚未真机验收。
- 证据目录：`build/validation/xr-hands-stage1-5286ad7d3a1540cb8b9fb6a024095200/`。
  日志含 `SPATIALAI_STAGE1_PASS contractChecks=307 playSessions=2`，Direct3D11 渲染的
  `simulation-preview.png` 已人工查看，文字与左右手骨架清晰。
- CoreChecks 回归 **458 项通过**；原有编辑器下 `SpatialHandTracking.FrameUpdated` 的 CS0067 警告保留。
- 从交付的 UPM sample 文件直接导入场景再次通过相同检查（无图形模式），证据目录
  `build/validation/xr-hands-stage1-7ab2aa8dd98349b3bd07d75e7cddbb24/`，确认脚本 GUID 与样例引用有效。
  基础包独立导入及 Cardboard 共存最小工程回归通过，证据目录
  `build/validation/xr-hands-stage0-8b6325358734461487a8722d059c6b35/`。
- 最终 Provider 对无效速度分量归零后，同一组 sample/状态/生命周期检查再次通过；最终日志目录
  `build/validation/xr-hands-stage1-a7f962641fc149659681d8b148716383/`，仍为 307 项检查、两次 Play。
- 用户运行步骤、Provider 状态规则与边界见 [阶段 1 文档](UNITY_XR_HANDS_STAGE1.md)。
  此阶段仅接受 Simulation 帧，不将 UVC 图像关键点发布为三维手，也未验证 Unity 官方 HandVisualizer 的网格样例。
  未构建/安装新 APK，真实精度、曝光至显示延迟、Android 长期稳定性仍留后续阶段。

## Unity XR Hands 阶段 0（2026-10-09）

- 新增 `SpatialAI.Core` 空间手部契约：固定 26 关节、左右手、米制 tracking origin、逐项有效/估算标记、
  来源与时钟域、归零版本及不可变快照；保留原有图像关键点与手势接口。
- 新增可选 UPM 包 `com.spatialai.xr-hands` / 程序集 `SpatialAI.XRHands`，固定 XR Hands 1.4.3、
  Input System 1.7.0、XR Management 4.4.0。不向基础 SDK 增加 XR Hands 依赖。
- `dotnet run --project tests/CoreChecks/CoreChecks.csproj`：458 项通过，其中新增 25 项空间手部契约检查。
- Unity **2022.3.14f1** 的两个严格最小新工程通过编译及 `Stage0CompileProbe.Run`：
  基础包单独导入未引入 XR Hands；配套包与本地 Cardboard 同时导入，26 个标准关节及全部 21 点对应验证通过，
  5 个缺失点明确标识，左右手对应正确，未创建手部子系统。
- 共存验证证据目录：`build/validation/xr-hands-stage0-02ad55f060aa479aadd04d89f3aa1cf5/`。
  两个日志均含 `SPATIALAI_STAGE0_PASS Unity=2022.3.14f1`；项目/缓存不进入 Git。
  实际传递依赖为 XR Core Utils 2.2.3、Mathematics 1.2.6、Legacy Input Helpers 2.1.10；共存时显式引入 UGUI 1.0.0。
- 不安装 Cardboard 的独立适配包验证也通过；证据目录
  `build/validation/xr-hands-stage0-17cf36411f6f45bd8b7a091261e5d257/`，验证了配套包没有隐藏的 Cardboard/UGUI 依赖。
- 最小工程发现 Cardboard Reticle 的 `EventSystems` 需要 UGUI；验证脚本已补齐此依赖。
  保留现有编辑器下 `SpatialHandTracking.FrameUpdated` 未使用的 CS0067 警告，无新增编译错误。
- 坐标变换、21→26 关节表、逐项能力声明、后续误差目标与测量方法见
  [阶段 0 契约文档](UNITY_XR_HANDS_STAGE0.md)。尚未运行模拟 Provider、真实三维转换或 XRI 交互，
  未构建/安装新 APK，真实精度及光学对位不属于此次编译验收。

## 小米 UVC 与手势性能（2026-10-09，当前）

HXQ UVC 采集/预览已通，手动曝光 16.6 ms 时可达到约 30 FPS；用户对照 33.3 ms 约 30 FPS、66.6 ms 约 15 FPS。
自动曝光下帧率随画面变化，但曝光时间读回不变，固件报告与实际曝光的关系仍待确认；曝光优先级不支持。
手势已改为 VIDEO + GPU、最多两只手，用户有手记录约 22～24 FPS、推理 35～43 ms，较此前 CPU 约 8～10 FPS 改善。
场景/手势开关、手动曝光设置及读回、GPU 场景重建已实机验证，长期稳定性、识别质量和稳定 30 FPS 尚未验收。
当前制品对应 Android 提交 a7e8ddc3ac3456aa97f1fa421576a4354807e14e，SDK 保留锁文件和导入元数据，生成的手势 AAR 不进入 Git。完整命令、源码/构建入口、证据边界与下一步计划见
[UVC 与手势识别接续文档](UVC_HAND_TRACKING_PROGRESS.md)。以下较早 RhinoX IMAGE 模式记录保留历史状态。

## 小米手机光学与尺度验收（2026-10-09，暂缓）

v6 手机安装及视图补偿完成后，用户反馈当前显示正常。本轮修正了转头、抬头和侧倾的显示响应；AA 像素布局此前已检查。物理尺度、实际镜片参数、双眼融合与畸变仍未完成量化验收，配置的验证标记保持未验证。

已有工作、装配条件、代码入口和恢复执行步骤见 [小米光学与尺度验收计划](XIAOMI_OPTICS_SCALE_VALIDATION.md)。该问题当前暂缓，下一阶段优先处理小米 UVC 画面与手势问题。

## 0.2.0-preview.9 手势故障恢复（2026-09-30）

- preview.8 在 RhinoX 真机上安装并加载 `gesture_recognizer.task` 成功；无手时约 20–28 FPS。用户伸手后反馈卡顿、手势消失。日志确认 MediaPipe 视频模式的 `ConcatenateTensorVectorCalculator` 重复抛出 `Packet isn't the sole owner of the holder`，并每帧重试。
- preview.9 将分类器切换到图像模式，并在第一次推理失败时关闭分类器、切换到原先双手 Hand Landmarker；切换后 `gestureSource` 标明 `landmarks-fallback`，Demo 显示 `HANDS ONLY`。故障回退保证骨架与捏合路径不会因分类器内部错误持续重试，但分类类别仍需真机有手场景验收。
- Android AAR 与 Unity 2022 Android APK 编译通过，AAR 的 SHA-256 为 `7b30ef330152d275a0db5c5b1424eb8af71daa2df954342d27c1df91fde99dc2`；本地 UPM 候选包位于 `dist/0.2.0-preview.9/unity/`。构建时设备已断开 ADB，preview.9 的有手场景实测尚未完成。Demo 用单块深色双目面板显示 XR、左右手类别和计数，待佩戴检查可读性。
- 随后设备 `03D11C420163` 重新连接，preview.9 APK 安装成功。有手日志持续为 `mode=gesture`，一只手约 16–17 FPS、两只手约 12 FPS，未见旧版图错误或回退。截图 `build/screenshots/spatial-gesture-preview9-live.png`（本机忽略的构建目录）显示骨架、单块深色面板、`Right: None 0.81 PINCH`、`Pinch 10 Select 1`；分类接口、捏合和选中计数在该场景同时工作。用户佩戴测试确认画面流畅，张掌和握拳时类别会变化；各类别准确率及长时间稳定性尚未量化。

## 0.2.0-preview.8 手势分类接口（2026-09-30）

- 用户确认上一版两只手均可识别。本版 Android Unity 桥接改用 MediaPipe Gesture Recognizer，一次推理输出每只手的 21 点、手别、类别和置信度，仍配置最多两只手；`Pinching` 与 Demo `Select` 保持原规则。
- Android AAR 从 `spatial-ai-android` 提交 `4febce284506a94d607513e23653025ff27803cc` 构建通过，SHA-256 为 `5c487032f7f8d4de1aecadfee237f32bb085ffcf532257ce41dafeb619b342ff`；官方手势模型校验值记录在 lock 文件，模型随 AAR 进入 APK。
- Unity 2022.3.14f1c1 消费工程的 Android APK 构建通过；本地 UPM 候选包位于 `dist/0.2.0-preview.8/unity/`。启动日志、实际类别与置信度、双手同时分类、帧率及头显中文字布局仍待真机检查；构建时 ADB 未连接。

## 捏合交互接口预览（2026-09-30）

`HelloSpatialXR` 通过 SDK 的 `SpatialHandTracking.FrameUpdated` 事件消费 RGB 手帧，显示指尖指向、目标悬停和捏合选中计数。Unity 2022 Android APK 已构建并安装到 RhinoX 开发设备 `03D11C420163`。首次安装发现示例场景在裁剪 `SphereCollider` 后初始化失败；已改为直接生成纯显示球体，重新构建、安装并验证启动日志出现 `SpatialAI hand interaction demo ready`，未再出现组件或空引用错误。无手状态下 MediaPipe 持续输出约 20–27 FPS。手到目标的真机命中率、延迟、左右手标识及重复触发仍待佩戴实测。此示例验证 Spatial AI 自有事件接口，不代表 `XRHandSubsystem` 或 XRI 标准输入已接入。

`dist/0.2.0-preview.7/` 是本次修复前生成的候选包；当前交互测试请使用源码工程或已安装的最新 APK，后续打包时递增版本号。

## 当前开发基线（2026-09-30）

- Android CameraService、MediaPipe 桥接、相机与手势诊断工程已拆到 `visor-x/spatial-ai-android`，提交 `31e528b48afd0fcc2dd49b5402aa5ec400e62045`。独立仓库的手势 APK、AAR 和相机诊断 APK 均构建通过；新 AAR 的 SHA-256 与当前 Unity 包内 AAR 完全一致，锁定信息见 `third_party/spatial-ai-android.lock.json`。
- Unity 2022.3.14f1c1 的 `samples/unity/SpatialXR/BuildAndroid.ps1` 已完成编辑器构建，APK 为 `build/samples/android/spatialxr.apk`；已在 RhinoX 开发设备 `03D11C420163` 安装并启动。构建输出与设备日志保存在忽略的 `build/` 中。
- Cardboard XR Display/Input 已启动；设备日志报告 RhinoX Pro Sharp 畸变网格每眼 2601 顶点已启用。实际光学等效、逐台标定及运动下画质仍需人工验收。
- RGB Camera2 ID 2 使用 1440×1080 帧；MediaPipe 在 384×288 图像上运行。无手时设备日志约 22–27 FPS，有手时此前测得约 17–19 FPS。示例用 RGB 标定参数和候选 RGB→eye 外参绘制骨架，手部深度按手掌图像尺寸近似估算。近远两档合成射线检查已通过，实际近远尺度仍待佩戴复测。
- iOS、Unity 6、团结、独立 3DoF 手柄、Unity XR Hands、手机兼容列表均未完成验收。以下较早日期的记录保留当时状态；其中“无 APK”“无设备”不是当前结论。

## Unity 2022 Android APK 构建（2026-09-28）

Unity 2022.3.14f1c1 的菜单构建先后暴露两项 Gradle 问题：Cardboard v1.35.0 AAR 使用 Java 17 字节码，旧 D8 无法处理；切换到 v1.30.1 后，缺少 AppCompat 依赖导致 `Theme.AppCompat.NoActionBar` 找不到。已为 Unity 2022 示例固定 v1.30.1，加入 Cardboard 官方要求的 AndroidX、Vision、Material、protobuf 依赖。依赖要求编译 SDK 至少 34；旧 AGP 的 AAPT2 无法读取 SDK 35 平台资源，因此本示例选择 SDK 34。

在 Unity 已生成的 Android Gradle 工程上用 SDK Platform 34 / Build Tools 30.0.3 执行 `assembleRelease`，结果为 `BUILD SUCCESSFUL in 29s`。APK 位于 `build/samples/android/spatialxr.apk`，大小 21,126,534 字节，SHA-256 为 `d3eaefc4758049dd0f1b5a951c809eb0d819cf4e4499651db9f00a2751401a46`；`apksigner verify --verbose` 通过，使用 v2 签名。日志为 `build/tests/gradle-cardboard-1301-sdk34.log`。本次打包沿用 Unity 已生成的场景与 IL2CPP 产物；新加的 `SpatialXRAndroidGradle` 回调已用 Unity 2022 程序集独立编译（0 警告、0 错误），仍需在 Unity 菜单构建中验证回调执行及真机画面。

## 0.2.0-preview.4 XR Camera 修正（2026-09-28）

现有 XR Sample 场景及场景生成器已加 Input System `Tracked Pose Driver`，读取 `<XRHMD>/centerEyeRotation`，固定相机高度以保持 3DoF 语义。编辑器内提供 `Spatial AI > Repair XR Sample Camera` 修复已经打开的场景。当前 Unity 2022 编辑器日志显示 `SpatialAI.HelloSpatialXR.Editor` 重新编译、场景重新导入，未见 C# 错误；仍未完成 Android 真机头姿验收。候选包位于 `dist/0.2.0-preview.4/unity/`。

再次尝试构建 Android APK：复制消费工程的 `Assets/Packages/ProjectSettings` 到 `build/tests/unity-spatialxr-apk-20260928/`，在 Unity 2022.3.14f1c1 批处理模式运行 `SpatialXRSetup.BuildAndroidSample`。Package Manager 启动时返回 `The "path" argument must be of type string. Received undefined`，尚未进入脚本或 Gradle；即使隔离副本只保留 Unity 内建包，也出现相同错误，说明该批处理环境的包解析故障不是由 Cardboard/Spatial AI 依赖单独引起。日志：`build/tests/unity-spatialxr-apk-20260928.log`、`build/tests/unity-upm-isolation-builtin.log`。本机 SDK 只有 API 31/32，Cardboard AAR 仍要求 API 35；`adb devices` 无在线设备。此次没有生成 APK，也未进行真机测试。已在打开的消费工程中补充 `Spatial AI > Build Android XR Sample` 菜单，供编辑器内构建验证。

## 0.2.0-preview.3 iOS 接入入口（2026-09-28）

| 检查 | 结果 | 边界与证据 |
| --- | --- | --- |
| Cardboard iOS 文件 | 已检出 iOS 原生静态库及 Unity 插件导入元数据 | `third_party/checkouts/cardboard-xr-plugin/Runtime/iOS/`；未修改上游插件 |
| Core 与包级 C# 引用编译 | 25 项 Core 检查通过；Unity 引用编译 0 警告、0 错误 | `dotnet run --project tests/CoreChecks/CoreChecks.csproj` 与 `tests/UnityCompile/`；不覆盖消费工程的 Editor 脚本 |
| iOS XR 配置与 Xcode 导出入口 | 源码已加入，未在 Unity 编辑器内验证 | `samples/unity/SpatialXR/Assets/Editor/SpatialXRSetup.cs`；本机 Unity 2022 Package Manager 在解析本地包时报告 `The "path" argument must be of type string. Received undefined`，见 `build/tests/unity-spatialxr-ios-config-retry.log` |
| iPhone/RhinoX 实机 | 未执行 | 当前 Windows 机器没有 macOS/Xcode/iPhone 验证环境；外接眼镜的 iOS 协议与显示仍未接入 |
| UPM 候选包 | 打包并核对 SHA-256 | `dist/0.2.0-preview.3/unity/`；本地候选，未正式发布 |

## 0.2.0-preview.2 Unity XR 基线（2026-09-28）

| 检查 | 结果 | 边界与证据 |
| --- | --- | --- |
| Cardboard 本地源码 | v1.35.0 两个固定提交校验通过 | `third_party/cardboard.lock.json`；Unity 插件完整 Runtime/Editor/Resources/Android AAR 已检出，未改上游源文件 |
| Core/原生 ABI | 26 项 Core 检查及 1/1 C ABI 测试通过 | `tools/Validate.ps1`；覆盖设备 ID 精确匹配、未知/重复 ID 与未标定 Profile 拒绝 |
| Unity 2022.3.14f1c1 消费工程 | 两个本地 UPM 包导入、Cardboard 适配层及 XR Sample 编译、场景生成、Android XR Loader 配置通过 | `samples/unity/SpatialXR/`；最终编译日志 `build/tests/unity-spatialxr-preview2-clean.log`，退出码 0 |
| 独立 UPM 导入 | 无 Cardboard 依赖时 Spatial AI 包单独导入并编译通过 | `build/tests/unity-no-cardboard-clean.log`，退出码 0；条件编译适配层未启用 |
| Unity Profile 资源 | `Resources` 中的 RhinoX 参考 Profile 被加载并按 `RhinoX` ID 命中，同时明确拒绝硬件使用 | `build/tests/unity-spatialxr-profile-resource.log`，退出码 0 |
| Android APK | 未生成 | Unity 的托管脚本与 IL2CPP 编译通过；Gradle `:launcher:checkReleaseAarMetadata` 拒绝：Cardboard AAR 要求 `compileSdkVersion >= 35`，本机 Unity 2022 SDK 只有 31/32。SDK Manager 要求接受 Android SDK 许可；未替用户接受，也未安装 SDK 35 |
| UPM 候选包 | 打包与 SHA-256 核对通过 | `dist/0.2.0-preview.2/unity/`；本地候选，未正式发布。先前缺少内建模块声明的 preview.1 已移到 `build/legacy/withdrawn-0.2.0-preview.1/` |
| 真机 XR | 未执行 | 当前 `adb devices` 无在线设备；双目画面、头姿方向、RhinoX 光学及手柄均未验收 |

Cardboard Sample 使用其自身的 Viewer 参数和 XR Provider。Spatial AI 的 RhinoX 参考 Profile 仅匹配并报告状态，没有接入显示或畸变。后续需在具备 SDK 35 的工具链完成 APK 构建，再做设备验收。旧版 `SpatialRuntimeHost` 的硬件 Backend 仍返回 Unsupported。

## 0.1.0-preview.1 历史记录

## 构建输出目录更新（2026-09-28）

源码和构建脚本已归位，构建输出按平台和组件划分；旧产物已移入 `build/legacy/pre-source-layout/` 保存，没有删除。

| 构建脚本 | 新默认输出 | 验证 |
| --- | --- | --- |
| `Validate.ps1` | `build/native/host/` | C ABI 测试 1/1、Core 检查 20/20 通过 |
| `BuildAndroid.ps1` | `build/native/android/arm64-v8a/spatial-runtime/libspatial_runtime.so` | NDK 编译通过；仍为不支持硬件的基础库 |
| `BuildCardboard.ps1` | `build/native/android/arm64-v8a/cardboard/libGfxPluginCardboard.so` | 编译、AArch64 ELF 和关键导出检查通过 |
| `BuildImuDiagnostic.ps1` | `build/native/android/arm64-v8a/imu-diagnostic/libspatial_imu_diagnostic.so` 与 `build/samples/android/imu-diagnostic/spatial-ai-imu-diagnostic.apk` | 编译、APK 签名验证通过；新路径 APK 未重做真机交互验收 |
| `Pack.ps1` | `build/staging/unity/` → `dist/0.1.0-preview.1/unity/` | 本地候选包已生成，tgz 包根与 SHA-256 核对通过；不是线上发布 |

本次整理后已重新执行核心测试、Android 原生库、Cardboard 原生库和 IMU 诊断 APK 构建。没有将上述原生 `.so` 自动加入 Unity UPM 包。Cardboard 与 Spatial Runtime 的功能集成仍待完成。

最新补充：2026-09-28 已在 RhinoX 真机运行独立 IMU 诊断 APK；详见本文末尾。下文 2026-09-27 的“无设备”是当日历史状态。

日期：2026-09-27，Windows x64。

| 检查 | 结果 | 实际覆盖 |
| --- | --- | --- |
| .NET 8 CoreChecks + native probe | 20 项通过 | 会话生命周期、模拟姿态、扳机边沿、暂停恢复、设备失效后的旧数据清空、未标定光学配置拒绝、C# 调用原生 ABI |
| CMake / MinGW 7.3 / C ABI 测试 | 1 个测试程序通过 | ABI 版本与结构大小、参数错误、资源创建释放、未实现硬件返回 Unsupported、无效输出清空 |
| Unity 程序集引用编译 | 通过，0 警告 / 0 错误 | 基于本机 Unity 2022.3.14f1c1 引用，编译 Core/Host/Editor/Sample |
| Unity 2022.3.14f1c1 实际导入 | 通过，进程退出码 0 | 独立项目从本地包加载，实际 asmdef 编译，导入 Sample shader，在编辑器内运行 Core 并生成 HelloSpatial.unity |
| Android NDK 23.1.7779620 / API 26 / arm64-v8a | 编译通过 | 产出原生基础库 libspatial_runtime.so；未安装到手机运行 |
| UPM 包结构 | 已检查 | package.json、asmdef、Sample 路径、稳定资产 GUID、以 package/ 为根的 tgz |

未完成：Unity 6 和团结引擎运行认证、Play Mode 交互及画面人工验收、APK 出包/安装、真实双目显示、设备输入、目标手机兼容性、Cardboard 与 Spatial Runtime 的运行集成。

本次启动进程缺失 ALLUSERSPROFILE 与 PROCESSOR_ARCHITECTURE，分别影响旧版 Unity 包管理器和 NDK 主机识别。验证时仅在当前进程中补入 CommonApplicationData 路径与 AMD64，并使用新的 Android 构建目录；未修改系统环境变量。首次失败日志不代表包编译失败，重试后实际导入成功。

当时的测试工程已归档在 `build/legacy/pre-source-layout/unity-smoke/`，模拟场景在其 `Assets/HelloSpatial.unity`。该工程使用 Unity 2022.3.14f1c1；它是辅助验证工程，不能当作 Unity 6 或团结引擎认证样例。当时的候选包在 `dist/0.1.0-preview.1/unity/`；旧包归档在 `build/legacy/pre-source-layout/`，仅作历史记录。

## Cardboard 构建基线补充（同日）

- `FetchCardboard.ps1`：两个固定提交、远端来源和无改动状态均通过校验；稀疏检出已执行。
- `BuildCardboard.ps1`：使用 NDK 23.1.7779620 编译未修改的 v1.35.0，Release / API 26 / arm64-v8a / 静态 C++ runtime；Unity provider、GLES3、Vulkan 均开启。
- ELF 架构检查及 8 个关键导出检查通过；动态依赖未出现 `libc++_shared.so`。
- 当日产物已归档在 `build/legacy/pre-source-layout/cardboard-android-arm64/`。整理后的产物在 `build/native/android/arm64-v8a/cardboard/`，完整提交、文件 SHA-256 与动态库依赖写入同目录 `build-report.json`，许可证为 `LICENSE.cardboard`。
- ADB 设备列表为空，未运行实机测试；未构建 AAR/APK；未连接 SpatialSession。UPM 包代码和版本未变。
- 上游指定 NDK 29.0.14206865；当前较旧 NDK 的编译成功不能替代发布工具链和现代 Android 内存页兼容验收。

## RhinoX 真机 IMU 基线（2026-09-28）

| 项目 | 实测结果 |
| --- | --- |
| 设备 | ADB 型号 RhinoX，序列号 03P11D14106Y，Android 10 / API 29，arm64 |
| APK | 独立 com.spatialai.diagnostics.imu，签名验证、安装与启动成功 |
| 实际算法链路 | Android SensorManager → JNI → 未修改的 Cardboard v1.35.0 EKF |
| 传感器 | icm4x6xx 加速度计和未校准陀螺仪，申请周期 10 ms，日志计数约 100 Hz |
| 保存的有效状态记录 | 87 条 fresh=true / finite=true；样本年龄 5–16 ms；四元数模长平方约 1 |
| 暂停与重新进入 | 暂停释放日志存在；重新进入时 PID 从 4680 变为 4812，重新初始化后恢复采样 |
| 页面 | 4320×2160 截图显示左右重复的诊断信息；不是双目渲染验收 |

结果与截图：`build/legacy/pre-source-layout/imu-diagnostic/device-20260928/summary.json`、`imu.log`、`screen.png`。新 APK：`build/samples/android/imu-diagnostic/spatial-ai-imu-diagnostic.apk`。构建入口：`tools/BuildImuDiagnostic.ps1`。

尚未验收：用户透过镜片看到的画面、真实转动方向、头戴标定、同进程暂停恢复、长时间漂移、端到端延迟、Unity/团结 Runtime 接入、光学畸变和双目显示。本次证明真实传感器可以驱动 Cardboard EKF，不代表完整 Cardboard AAR 或官方样例已运行，也不代表手机与眼镜的分体链路已打通。

## 2026-10-10：Unity XR Hands 阶段 2（软件验证）

后续调整：标定参数可选，缺失时提示并补开发预览默认值；已有参数优先使用，精度验收暂缓。
最新 CoreChecks **976 项**、Unity 2022.3.14f1 **545 项**通过，包含无配置、空 JSON、部分参数、
估算状态和提示只记录一次。最新日志：
`build/validation/xr-hands-stage2-fa0ae10cda294ec18e903c682f25cc91/verification.log`。
标定工具和 JSON 加载保留，非有限数据、相机/尺寸不匹配等实际参数错误仍拒绝。
真实 Provider 可先使用默认参数推进；以下 960/524 与 APK 记录为此前采集版本的验证。

- .NET CoreChecks 960 项通过；新阶段新增 502 项几何/尺度/有效性检查。
- Unity 2022.3.14f1，独立基础 SDK 工程 524 项通过：
  `build/validation/xr-hands-stage2-2ca5dd8c54d7414c97f290caee754402/verification.log`。
  包含 JSON、旧帧/左右手/时钟转换、采集场景序列化，不依赖 XR Hands/Cardboard。
- Python/OpenCV 4.11.0/NumPy 2.2.6，4 个工具测试通过；25 张合成图角点检测/内参拟合，
  训练 RMS 0.123 px、held-out 最大 0.155 px；外参、报告、退化与无效样本检查通过。
- HXQ 已固定、用户确认可采集。尚无实际标定结果和手部误差报告，阶段 2 实机验收未完成。
- 软件接口和工具详见 [阶段 2 文档](UNITY_XR_HANDS_STAGE2.md)；真实 UVC Provider 仍属于下一阶段。
- 标定采集 APK 构建成功，APK v2 签名验证通过；53,520,991 字节，
  `build/samples/android/spatialxr-stage2-calibration.apk`，SHA-256
  `ff18b56c6212f39b443d8933c887b67d5074ae29a6c035dbb64e1c6be2a9beba`。
  构建日志 `build/logs/xr-hands-stage2-apk-final.log`；ADB 无在线设备，尚未安装/验证采集接口。
- 模拟 Provider 回归 307 项、两轮 Play/Stop 通过：
  `build/validation/xr-hands-stage1-aeb0e8c910f842829a440cb7d78e7686/verification.log`。

### 同日补充：重新构建与小米安装验证

- 当前 APK：`build/samples/android/spatialxr-stage2-optional-calibration.apk`，53,522,459 字节，APK v2 签名验证通过。
  SHA-256：`d6bd82c7e066804ef6665746f6db5b2b1328293de56359fa23628c9ea5fe62bc`；
  构建日志：`build/logs/xr-hands-stage2-optional-calibration-apk.log`。
- `10.168.2.104:5555`，小米 2206123SC：`adb install -r` 成功，Unity Activity 启动成功。
  广播切换到 `SpatialUvcCalibration` 成功，`scene=capture` 保存 PNG 和 JSON 成功。
- 保存图像 384×288，JSON 原始模式 640×480，相机 `uvc:11cb:281b`，全视野且无旋转/镜像。
  证据：`build/validation/stage2-device-20261010/`；这组图像仅验证保存，不作为标定数据集。
- 手动曝光 16.6 ms，UVC 约 30 fps；VIDEO/GPU 推理约 21～26 fps（无手画面）。
  已切回手势场景；人工画面、单/双手识别、Capture 按钮及暂停恢复仍待用户确认。
- 示例 APK 未消费阶段 2 三维转换器，也未接入真实 UVC XR Hands Provider；缺标定继续转换由软件测试覆盖。
  精度标定与镜片空间对位按用户决定暂缓，不阻止阶段 3 开发。

### 同日补充：UVC 结果驱动独立三维骨架

- 示例手机分支现在启用 `HandTrackingDemo`，UVC 分支消费阶段 2 转换器；三维骨架独立显示，
  手势场景不显示预览矩形，采集场景保留预览。仅为头部局部空间的估算预览，真实 XR Hands Provider 未接入。
- Unity 编辑器集成验证 104 项通过，覆盖无标定双手、标签顺序、加载参数/左右手修正、单手退化、超时及启停：
  `build/logs/uvc-3d-hand-demo-check.log`。
- `build/samples/android/spatialxr-uvc-3d-hands.apk` 构建成功，53,524,071 字节，v2 签名校验通过，
  SHA-256：`cbd381c7051fcc952fb3277e5d1b79a1716f4c9382985f20f6f2f06a5b93d0f1`。
  已安装到 `10.168.2.104:5555` 并启动；构建日志 `build/logs/uvc-3d-hands-apk-final.log`。
- 实机双手动作、骨架实际可见性及镜片中左右对应待用户确认；精度验收继续暂缓。

### 同日补充：镜片中手部左右反向

- 用户通过镜片确认只有左右反向、上下正常。小米 Host 的 `reflectHandDisplayX=true`
  在 UVC 骨架与捏合指针展示层执行 `(-x,y,z)`；物理关节、相机参数、手别和 IMU 不变。
- Unity 集成验证 150 项通过，包含 42 个点的 X 反射/YZ 保持、原始点保留、标签及指针一致性：
  `build/logs/uvc-3d-hand-mirror-check.log`。
- `build/samples/android/spatialxr-uvc-3d-hands-mirror.apk`，53,524,411 字节，v2 签名校验通过，
  SHA-256：`aacdac916da3aeab59bf36de8f514cc642b34a691d7c27b90e2555e3ee9c502a`。
  构建日志 `build/logs/uvc-3d-hands-mirror-apk.log`；已安装到 `10.168.2.104:5555`，启动并切到手势场景。
- 补偿轴依据当前安装、镜片和输出方向组合的实测反馈；新版镜片中左右跟随、上下保持和捏合指针待用户复测。

### 同日补充：真实左右手标签反向

- 用户确认骨架移动方向正确，但真实左手显示 Right/青色，真实右手显示 Left/绿色。
  小米/HXQ 的无标定预览启用 `uvcSwapHandedness=true`，统一修正转换器、颜色、文字、手势和捏合映射；
  原始识别帧保留，已有显示 X 反射继续启用，不再次镜像位置。
- Unity 集成验证 161 项通过：左右单手与双手不同手势、原始标签保留、颜色/文字一致、
  显示坐标不变及 JSON 配置优先：`build/logs/uvc-3d-hand-handedness-check.log`。
- `build/samples/android/spatialxr-uvc-3d-hands-handedness.apk`，53,525,827 字节，v2 签名校验通过。
  SHA-256：`fa9b4fd6d37f3bd908a8bedc3aa24b6cad81794e359622a9225cc04747e6f809`。
  构建日志 `build/logs/uvc-3d-hands-handedness-apk.log`；已安装到 `10.168.2.104:5555`，启动并切到手势场景。
- 人工复测：只伸真实左手应为绿色/Left，只伸真实右手应为青色/Right，双手不同动作分别对应。
  赋值标定 JSON 后使用其中 `swapHandedness`（包括 false），此安装的配置应填 true。

### 2026-10-10：翻手稳定与问题帧诊断

- 示例增加连续身份关联，保留原始标签；短暂几何失败仅在同一只手仍被检测时保留最后有效骨架最多 150 ms。
  严格 SDK 转换仍输出 Untracked，保留画面不能触发目标选择；持续拒绝、手消失或身份改变会隐藏骨架。
- Android 独立传递原始手别置信度，UI 显示 G/rawH 与 LOCK；SpatialHandsDiag 记录实际标签、两种分数、拒绝原因及 21 点分片快照。
- 核心 976 项、阶段 2 转换 545 项、Unity 示例集成 181 项检查通过。
  日志：build/logs/uvc-hand-core-regression.log、uvc-hand-stage2-regression.log、uvc-hand-stability-check-final.log。
- build/samples/android/spatialxr-uvc-hand-diagnostics.apk 构建成功，53,532,371 字节，v2 签名通过。
  SHA-256：430814256f91ed7e59e28ec2d5518a0f67b334115920b92167e899b2fa1ae296。
  已安装到 10.168.2.104:5555 并启动；构建日志 build/logs/uvc-hand-diagnostics-apk.log。
- 实际侧掌稳定程度和持续几何拒绝原因待用户复现日志确认；抓取方法见 [翻手诊断文档](UVC_HAND_DIAGNOSTICS.md)。

实机启动检查：示例进入 HelloSpatialXR，摄像头首次启动后发生 USB 断开；重连时前台为
系统 UsbPermissionActivity，当前等待用户在手机允许 USB 相机访问。尚未获得本轮实机推理帧，
因此新增原始手别分数与问题快照的实机输出待授权后复现确认。启动记录：build/logs/uvc-hand-diagnostics-device.log。

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

本轮 APK：build/samples/android/spatialxr-uvc-side-palm.apk，53,533,407 字节，v2 签名校验通过。
SHA-256：1eba64ef2e99b1aa5c2c0156ef5d3db86e8c20f43dad5105e4d879a6ecc43b53。
已安装到 10.168.2.104:5555 并启动；构建日志 build/logs/uvc-side-palm-apk.log。
实机启动检查通过：手势场景运行，UVC 640x480 @30，手动曝光 16.6 ms，captureFps 约 30.0，
previewFps 约 29.9；新增 SpatialHandsDiag 相机距离字段已输出。检查时未检测到手，实际翻手与
实机快照完整性仍需用户复测，不能以编辑器检查代替。日志：build/logs/uvc-side-palm-device.log。

### 手势远距离显示版

build/samples/android/spatialxr-uvc-far-display.apk 构建成功，53,534,715 字节，v2 签名校验通过，已安装到 10.168.2.104:5555。
SHA-256：83482650707b29f65da5159c9919de97dbcd03670ae2505a25b9065a7d604b0f。
构建日志：build/logs/uvc-far-display-apk.log。展示根节点倍率 3，UI 字体额外放大 25%，实际合相舒适度待用户复测。

## Unity XR Hands 阶段 3（2026-10-10）

真实 UVC 帧已通过 `SpatialAI-UVC-Hands` 发布到标准 `XRHandSubsystem`；
阶段 1 的模拟 Provider 保留。主线程快照、Dynamic/BeforeRender 复用、350ms 超时、
左右身份关联、相机失效及启停恢复均已接入。标准数据保留严格几何有效性，不使用旧预览的距离保留。

全新工程 96 项契约与 Play 模式 20 次启停检查通过：
build/validation/xr-hands-stage3-ea690cb6d0ad4662902e4174dd00733d/verification.log。
模拟 Provider 307 项回归与两次 Play 会话通过：
build/validation/xr-hands-stage1-9b7d9ff56f184f3d8f06ed4544caf1aa/verification.log。

最终 APK：build/samples/android/spatialxr-xr-hands-stage3.apk，54,611,483 字节，v2 签名通过。
SHA-256：44786035b6f56d79f53f069e25288abde484afb32b4eb63880feb79e5e2ab016。
构建日志：build/logs/xr-hands-stage3-final-apk.log。已安装到 10.168.2.104:5555。
识别开关、场景往返、前后台恢复冒烟通过；运行记录未发现 Unity/GPU 异常。
日志：build/logs/xr-hands-stage3-final-device.log。采集约 30FPS、启动时无手识别/发布约 19FPS；
恢复后稳定窗口识别约 23.5～24.7FPS、标准发布约 20.6～21.8FPS、渲染约 60FPS。
检查时未检测到手；完整单/双手采样、USB 拔插、长时间温升和真实异常回退路径待验收。
坐标精度与光学验收仍未通过。使用与后续步骤见 [阶段 3 文档](UNITY_XR_HANDS_STAGE3.md)。

### 用户实机复测补充（2026-10-10）

用户确认左右手骨架、移动方向、双手显示，以及手移出后消失/返回后恢复均正常。
USB 拔插后重新获取并人工确认权限，识别能够恢复；该项功能验证通过，免确认恢复尚未实现。
首次打开应用及 USB 重插需要确认权限的问题已记录为 UVC-PERM-001，按用户要求暂缓解决。
后续调查与回归范围见 [阶段 3 文档](UNITY_XR_HANDS_STAGE3.md#待处理uvc-授权流程uvc-perm-001)。
此次反馈不替代 60 秒性能采样、长时间温升、资源趋势及空间精度验收。

## Unity XR Hands 阶段 4（2026-10-10）

新增独立可选包 `com.spatialai.xr-interaction`，固定 XRI 2.5.4 与 Input System 1.7.0。
标准 XR Hands 关节驱动左右独立射线、捏合选择与世界空间 UI；包含动作资产、prefab 和示例场景。
迟滞与 50ms 去抖按新输入帧计算；跟踪丢失、缺关节、归零、禁用及暂停取消交互，恢复需先张开手。

全新工程通过 39 项契约检查、54 项标准 XRI 集成检查和 20 次设备启停；
合成输入产生 3 次真实 UI 点击，丢失/归零取消 UI 按压不产生点击，运行时无错误或异常。
验证日志：build/validation/xr-hands-stage4-9acb9a85afb44fdfb4a318ba25f6252e/verification.log。
包含 UPM 示例 prefab 导入、动作资产解析与示例组件生命周期检查。

最终 APK：build/samples/android/spatialxr-xri-stage4.apk，55,592,213 字节，v2 签名通过。
SHA-256：a9286706c210cb270baf10b7ec9a7bbaa84d23f488d88d346e8f239905f092c5。
构建日志：build/logs/xr-hands-stage4-layout-apk.log。已安装到 10.168.2.104:5555。
无摄像头显示、XR Hands/XRI/IMU 场景往返与前后台恢复检查通过；本应用进程未发现 Unity 错误或异常。
日志：build/logs/xr-hands-stage4-final-device.log；截图：build/logs/xri-stage4-final-screen.png。

用户明确暂不连接摄像头，真实手部瞄准、捏合、UI 点击、取消与恢复全部保留实机待验收。
自动检查不代替真实输入验收；示例仍为头部局部远距离展示，未验收固定世界交互或近距离抓取。
UVC-PERM-001 继续暂缓。使用及验收清单见 [阶段 4 文档](UNITY_XR_HANDS_STAGE4.md)。

### 同日补充：真实 UVC 与射线方向复测

用户接回 UVC 后，真实采集恢复约 30 FPS（640×480，手动曝光 16.6ms）；
VIDEO/GPU 识别约 19～23 FPS，稳定渲染约 60 FPS。识别开关、场景往返和前后台恢复真实输入通过。
真实右手捏合已产生 Right object 选择及正常释放记录；严格几何失效时产生取消记录。
记录：build/logs/xri-stage4-uvc-device.log、build/logs/xri-stage4-ray-user.log。
用户报告射线从手部向眼睛延伸，与掌面朝向无关；视觉方向尚未通过，不能以目标选择替代该项验收。

新增显式射线起点、可见线与检测同源、失效隐藏旧线条，以及每两秒 SpatialXRI aim 方向诊断。
39 项契约、57 项集成和 20 次启停检查通过：
build/validation/xr-hands-stage4-cda259d54df3467798530dd1381a4a82/verification.log。
诊断 APK：build/samples/android/spatialxr-xri-ray-diagnostics.apk，55,592,529 字节，v2 签名通过，已安装。
SHA-256：9e9c3b5b299cf513c0da20eb590d6122eff531621a567b774c65c5809a9e73b3。
构建日志：build/logs/xri-stage4-ray-diagnostics-apk.log。
安装后真实 UVC 再次采集约 30 FPS，标准发布约 19～20 FPS，稳定渲染约 60 FPS；采样时双手未跟踪。
运行记录：build/logs/xri-stage4-ray-diagnostics-device.log。
方向问题复测、左右/双手完整交互、UI 点击以及按压时丢失/恢复的人工验收仍未完成。

### 镜像肩部偏移修正

用户反馈左右射线分别向眼睛左/右侧延伸。检测朝向日志前向点积约 0.94～0.96，代码确认镜像后虚拟肩部交换了左右。
先映射掌部与头部到显示坐标，再按最终手别选择同侧肩部；LineRenderer 显式使用世界坐标，日志增加最终线段端点。
原始 XR Hands 关节与捏合距离保持不变；未直接反转 Z 轴。
39 项契约、62 项集成及 20 次启停检查通过，新增左右手镜像及三倍显示距离回归：
build/validation/xr-hands-stage4-d6e1da7d11f246039494dd359d959378/verification.log。
APK：build/samples/android/spatialxr-xri-reflected-shoulder.apk，55,596,021 字节，v2 签名通过，已安装。
SHA-256：dc1155ae7e69b0ff734b7f253a3b61cd308300d4b801095f399d3cf371641882。
构建：build/logs/xri-stage4-reflected-shoulder-apk.log；运行：build/logs/xri-stage4-reflected-shoulder-device.log。
进入 XRI 场景，UVC 真实采集恢复约 30 FPS。镜片中的射线朝向观感与完整真实交互仍待人工复测。

用户复测肩部修正后仍觉得朝眼睛延伸，单眼看也如此。最终线段端点日志仍显示向前，例如 Z=1.825→11.547。
增加终点箭头及沿实际线段从手向终点移动的箭头，不改变真实检测射线，不参与碰撞；方向观感继续待确认。
39 项契约、70 项集成及 20 次启停通过：
build/validation/xr-hands-stage4-852433af61b54635b4840273119f0998/verification.log。
包含箭头沿标准 XRI 线段朝向及取消后隐藏检查。
APK：build/samples/android/spatialxr-xri-direction-cues.apk，55,595,461 字节，v2 签名通过，已安装。
SHA-256：e98157896bb5eb40556a411328e9a24325e8021bbf88609e7bae2b0314b470f0。
构建：build/logs/xri-stage4-direction-cues-apk.log；运行：build/logs/xri-stage4-direction-cues-device.log。
进入 XRI 场景且真实 UVC 采集恢复约 30 FPS，实机方向与交互待确认。

用户随后直接看手机屏幕，反馈射线方向似乎正确，镜片中更像视觉错觉；与最终线段端点前向日志一致。
因此保持当前射线朝向；镜片深度/方向感转入已有光学待验收记录，具体光学原因未确定。
左右/双手完整选择、UI 点击、按压时跟踪丢失取消及恢复重新武装仍待人工确认。

### 捏合掉跟踪与掌部瞄准优化

用户反馈难以选中方块，捏合时 tracking lost。日志 build/logs/xri-stage4-pinch-loss.log 显示
MediaPipe 仍识别 hands=2，但 SDK 因掌轴比超过 1.6 频繁拒绝数据，不能全部认定为真实漏检。
新增可配置的当前帧估算策略，标准 Provider 默认限制仍为 1.6，UVC 交互示例明确使用 4。
保留缺关键点、掌部塌缩、严重退化、范围错误、相机失效和 350ms 过期检查；没有沿用丢失后的旧位置。
捏合尺度加入当前腕部到中指参考；UVC 示例用 HeadToPalm 瞄准，目标悬停变亮、选中变白。

982 项核心检查通过；标准 XR Hands 96 项契约、20 次启停回归通过：
build/validation/xr-hands-stage3-1ae652b5ed5e49ca98bb05728781013c/verification.log。
阶段 4 的 46 项契约、74 项交互、20 次启停通过：
build/validation/xr-hands-stage4-bbb656d1da624565a50b56dfad8d409b/verification.log。
新增当前帧宽容数据、策略重建、失效/过期清理，以及镜像掌部对准目标后的真实悬停/选择/释放验证。
APK：build/samples/android/spatialxr-xri-palm-pointer.apk，55,788,069 字节，v2 签名通过，已安装。
SHA-256：e7c8fc376cc12ae9b55286fc1790ff749e3ff7980d5fbd6b334ee491a208f9c6。
构建：build/logs/xri-stage4-palm-pointer-apk.log；运行：build/logs/xri-stage4-palm-pointer-device.log。
实机确认 aim=HeadToPalm、maxPalmAxisRatio=4，640×480 约 30 FPS，手动曝光 16.6ms。
轴比 2.056 的新鲜右手估算数据成功 acquired；改善后的完整瞄准、捏合保持及 UI 操作仍待人工确认。

用户复测确认捏合基本不再掉跟踪，白色瞄准标记重叠方块时，捏合一般可以选中。
日志已记录多次真实 hover/select 及正常 release，基本目标选择与捏合稳定性改善得到本轮反馈。
白色圆点对应 HeadToPalm 射线沿视线投影缩短后的标记；阈值判断不是指尖接触判断。
仍可见缺锚点、重复手别和轴比超过 4 的拒绝事件；UI 点击、双手完整操作及丢失/恢复仍未完整验收。

2026-10-10 食指射线：UVC 示例设置 `rayOrigin=IndexTip`，可见线和 XRI 碰撞同用食指尖起点，方向保留 HeadToPalm。
食指尖沿用显示反射及距离倍率，缺失时取消；模拟场景默认掌部起点保持原样。
编辑器验证通过 46 项契约、83 项集成检查、20 次生命周期循环、3 次模拟 UI 点击；日志为 `build/validation/xr-hands-stage4-714bfc958246468b86cbadcbf29b4955/verification.log`。
镜片中的方向感及食指尖随捏合移动时的瞄准稳定性待实机验收。

2026-10-10 视频复核后更新：食指尖起点加 HeadToPalm 在屏幕中仍向掌部收敛，UVC 示例改为 IndexExtension。
沿手腕到食指根部的视平面方向向外延伸，同时保持前向深度；捏合期间保持最后一次张开手的方向，起点仍随指尖更新。
失效清除方向保持，恢复仍先张开手；新日志字段为 `aimMode`、`aimHeld`。镜片中的最终效果仍需人工确认。
本轮 46 项契约、99 项集成检查、20 次生命周期循环、4 次模拟 UI 点击通过；日志 `build/validation/xr-hands-stage4-aca131f1084146d7a4e71e4fa82dc0f7/verification.log`。
`spatialxr-xri-index-extension.apk` 已安装至测试手机，启动确认 `aim=IndexExtension rayOrigin=IndexTip`。

用户确认捏合选择稳定，但白色标记仍给出靠近眼睛的感觉。本轮改为固定尺寸的白色移动标记及射线颜色的静止终点，去掉随距离放大。
保留已验证的食指瞄准、捏合保持和取消恢复逻辑；镜片中的远去方向感待复测。
本轮通过 46 项契约、105 项集成检查、20 次生命周期循环、4 次模拟 UI 点击；实际网格投影验证远去缩小，并检查距离变化不放大标记及终点颜色区分。
日志：`build/validation/xr-hands-stage4-f3e2b6701a0f45f787087eede951a7b8/verification.log`。

2026-10-12：UVC 示例从掌部发射，使用 HeadToPalm，下压 15°，最大仰角 0°；新增可调俯仰偏移、仰角上限及可选捏合方向保持。
最终验证通过掌部起点、压低角度、屏幕投影向下延伸、捏合选择保持和正常释放；日志 `build/validation/xr-hands-stage4-2d66bc1e9b8446679ddff477b0de22a1/verification.log`。镜片中的最终角度仍待人工验收。
本轮通过 46 项契约、111 项集成检查、20 次生命周期循环、4 次模拟 UI 点击；`spatialxr-xri-palm-lowered.apk` 已安装，启动配置确认掌部起点、-15° 偏移和 0° 仰角上限，UVC 已采集且手动曝光 16.6ms 回读匹配。

2026-10-12 后续调整：UVC 示例改为掌部起点、HeadForward 双手平行前向、上扬 3°；两个方块移至独立场景根节点，按启动时视线放置后保持坐标固定。
测试验证目标世界坐标/朝向在相机运动后不变、画面位置改变，以及目标根节点停用/恢复/销毁无残留。手和调试/UI 保留头部局部预览，真实镜片方向感待人工验证。
日志 `build/validation/xr-hands-stage4-9c9849f385a840a09934213aa61072e3/verification.log`。
本轮通过 46 项契约、120 项集成检查、20 次生命周期循环、4 次模拟 UI 点击；`spatialxr-xri-world-targets.apk` 已安装，启动确认 HeadForward、掌部起点、上扬 3°和 worldFixed 方块，UVC 采集及手动曝光回读正常。

### 2026-10-12：3DOF 食指 UI 与标准手势

当前默认交互取消可见射线、白球、骨架及可选方块，使用食指圆圈操作普通 Button。
应用手部接口为 XRHandSubsystem / XRHandTrackingEvents；手势由原样导入的官方 Gestures 样例 StaticHandGesture 配合 XRHandShape 资产提供，不增加 Spatial 手势事件接口。
验证日志：`build/validation/xr-hands-stage4-30c0de56a02e45a68e626be70ff8836f/verification.log`。
通过 46 项契约、142 项集成检查、20 次生命周期循环、7 次模拟 UI 点击；覆盖无可见线条、食指投影及控制器父节点旋转后圆圈位置、估算手指超过 UI 深度仍能命中、双手独立点击、丢失取消、恢复武装，以及官方握拳/张开/捏合事件和跟踪丢失反馈。
以上为模拟输入自动验证，真实手势阈值、镜片圆圈位置与点击舒适度仍待佩戴验收。UVC-PERM-001 与标定继续搁置。
完整接入与人工清单见 [阶段 4 文档](UNITY_XR_HANDS_STAGE4.md)。
最终 APK：`build/samples/android/spatialxr-xri-fingertip-ui.apk`；SHA256 `cfdccf76ffc3e5840b9af3c568e194283b265111cad5bc8ae94343358dae78a5`，v2 签名校验通过，已安装到 10.168.2.104:5555。
启动日志：`build/logs/xri-fingertip-ui-device.log`；截图：`build/logs/xri-fingertip-ui-device.png`。
标准手势样例的 7 个 C# / asmdef 文件已与 Unity 包内原始文件逐一校验一致。
UI 延迟到初始 XR 姿态更新后摆放，再保持世界坐标固定，避免在场景 Start 时沿尚未更新的相机方向摆放。
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

## 2026-10-12 默认 CPU、SDK 统一补偿与三项 FPS

- 阶段 3：109 项契约检查通过，20 次 Play 生命周期循环通过；合成输入覆盖身份稳定、侧掌当前关键点投影、150/120/350ms 到期、头姿只转换一次、缓存失效和标准 Untracked 下的仅显示补偿。
  日志：`build/validation/xr-hands-stage3-f4a1713149764ab9974eb98480a2cae9/verification-sdk-compensation.log`。
- 阶段 4：47 项契约、172 项集成检查通过，20 次生命周期循环、7 次合成 UI 点击通过；保留标准 XR Hands 手势与 XRI 交互、圆圈样式、随头目标、三项 FPS 和 CPU 默认值。
  日志：`build/validation/xr-hands-stage4-2e8da17e8f9d443e84fb14bd033b2287/verification-cpu-compensation.log`。
- 旧 `hands` 场景：191 项集成检查通过，包括共享 SDK 补偿、镜面/手别映射、无手时仍显示推理 FPS、停止来帧后清除骨架与悬停。
  日志：`build/logs/sdk-compensation-legacy-check.log`。
- Android AAR 重新构建并通过锁文件验证，SHA256 `a07aec9196901308b809f0823961146dd65c49680e00ae8b25fa616348f45e86`。
  来源为 Android 本地工作区，锁文件记录状态和 Java 源码哈希；生成 AAR 继续被 Git 忽略，不跟踪二进制。
- Unity 2022.3.14f1 APK 构建成功，v2 签名通过，已安装至 `10.168.2.104:5555`。
  制品：`build/samples/android/spatialxr-xri-cpu-sdk-compensation.apk`，56,033,385 字节；
  SHA256 `1d7665528f86f652cf14ac057b197087d19d22613992a7a204228412e96da847`。
  构建日志：`build/logs/xri-cpu-compensation-build.log`。
- 实机新进程默认 CPU，广播 CPU → GPU → CPU 均成功；创建、推理和资源关闭留在 `SpatialCamera-UnityHands` 消费线程。旧 `hands` 场景及返回 `xri` 正常启动。
  切换日志：`build/logs/cpu-gpu-sdk-compensation-switch.log`；最终 APK 冒烟：`build/logs/cpu-sdk-compensation-final-device.log`。
  无手观测 AI 约 17～24FPS / UVC 30FPS / Render 60FPS，不代表有手时的 CPU 性能或稳定性能验收。恢复 CPU 的 `xri` 场景供用户复测。

本次自动检查使用合成手部数据；实机检查期间为 hands=0。迁移后的侧掌、左右手、双手、捏合和镜片可读性仍需佩戴复测，不能用本次日志替代人工验收。

### 2026-10-12：阶段 5 交付与用户新工程

用户已确认阶段 4 最新 CPU 版本的基础真实交互与 SDK 补偿正常。阶段 5 四包发布归档、安装文档、源码/制品校验和新工程检查已生成；隔离工程交互 15 项、Play Mode 10 项、仅显示 18 项、升级 6 项检查通过，两个 SDK 消费工程 Android APK 构建成功。
发布包重新生成后四个 tgz 哈希一致。手机安装返回 INSTALL_FAILED_USER_RESTRICTED，新的消费工程实机显示/3DOF/UVC 验收尚未完成。
用户 D:/code/unity/spatialai-sdk-test-demo 已增加交互包、官方 Gestures、UVC 与模拟交互场景及 Android XR 配置；模拟运行检查通过，临时配置脚本已清理，使用说明在该工程 SPATIALAI_QUICKSTART.md。
制品、哈希、日志和验收边界见 [阶段 5 报告](UNITY_XR_HANDS_STAGE5.md)。

### 2026-10-12：基础包自动安装与用户确认

基础 SDK tgz 已内置 Editor 安装器及 Cardboard、XR Hands、XR Interaction 三个配套归档，取消独立 installer 制品。全新工程导入、旧 installer 升级移除、程序集重载、SHA-256 失败/重试与工程搬迁自动验证通过。用户确认最新合并包“验证没有问题了”，本轮软件接入和基础 SDK 消费工程验收完成；此前 APK 安装受限保留为历史结果，精度与长时间测试仍独立待办。
已完成文件及验证工具审核，保留项加入 SDK/Android 两仓库暂存区，用户确认后已删除 21 个被忽略的临时文件，尚未创建 commit。详见 [改动与暂存清单](UNITY_XR_HANDS_CHANGE_REVIEW.md)。

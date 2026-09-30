# 验证记录

## 捏合交互接口预览（2026-09-30）

`HelloSpatialXR` 现通过 SDK 的 `SpatialHandTracking.FrameUpdated` 事件消费 RGB 手帧，显示指尖指向、目标悬停和捏合选中计数。Unity 2022 Android APK 已构建通过；当前设备未连接 ADB，手到目标的真机命中率、延迟、左右手标识及重复触发仍待实测。此示例验证 Spatial AI 自有事件接口，不代表 `XRHandSubsystem` 或 XRI 标准输入已接入。

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

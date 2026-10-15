# Unity XR Hands 接入与 SDK 交付：改动、暂存和清理清单

日期：2026-10-12。本清单覆盖当前工作区相对两个仓库 HEAD 尚未提交的 XR Hands 接入、UVC 补偿与后续 SDK 包交付改动。保留项已加入暂存区；用户确认后，21 个临时文件已删除，它们原本被忽略、未暂存。本次不创建 commit、不推送。

## 当前进展

用户已确认真实左右手/双手、移出返回、USB 重连后授权恢复、基础捏合交互与 SDK 补偿正常；消费工程 APK 手势场景正常，配置 Android XR 后显示正常。最新基础 SDK tgz 自动安装三个配套包，用户反馈“验证没有问题了”。本轮阶段 0～5 软件接入与开发预览交付完成，详细记录见 [阶段 5](UNITY_XR_HANDS_STAGE5.md)。

应用使用 Unity XR / XRHandSubsystem / XRHandTrackingEvents / 官方 StaticHandGesture 与 XRI 的标准接口。默认 CPU/VIDEO，GPU 是 SDK 后端配置；Demo 保留 am broadcast 调试入口。显示补偿与有效跟踪分开，短暂保留的显示骨架不作为有效输入触发交互。3DOF 示例使用圆圈而非可见射线，保留骨架、方块、普通 UI 及中部带背景的 FPS/手势调试文字。

基础 `com.spatialai.sdk-0.2.0-preview.10.tgz` 内置 Editor 安装器及 Cardboard、XR Hands、XR Interaction 三个配套 tgz，独立 installer 已退出当前交付。新工程自动安装、旧入口升级、重载、校验失败/重试及工程搬迁验证通过。源码 checkout 不自动安装；用户仍需执行 Configure Android XR。

待办：USB 首次/重连权限确认优化、实际标定与米制尺度、小米光学与合相、长时间稳定性/温升与性能分布、跨硬件及其他 Unity/渲染管线认证。用户基础功能确认不替代这些量化验收。

## 保留的实现与资源

| 范围 | 主要文件/目录 | 修改目的 |
| --- | --- | --- |
| 基础手部契约与转换 | 基础包 Runtime/Core 的 HandContracts、MonocularHandConverter、UvcHandGeometry | 统一时间、坐标、26 点映射、有效性及单目估算能力 |
| SDK 手部处理 | Runtime/Unity 的 SpatialHandTracking、SpatialUvcHandsConverter、SpatialUvcHandProcessor、SpatialUvcHandStabilizer | CPU/GPU 配置、手别身份稳定、侧掌与漏检显示补偿、有效输入失效管理 |
| 设备与标定 | HostDeviceProfiles、小米 profile、SpatialUvcCalibrationRecorder、SpatialUvcCalibrationScene、docs/calibration | 反射/手别默认配置，标定采集、加载、模板与误差测量；缺少参数只提示 |
| XR Hands 配套包 | packages/com.spatialai.xr-hands | 标准 Provider/Subsystem、模拟/真实帧源、生命周期、可视化与可导入示例 |
| XRI 配套包 | packages/com.spatialai.xr-interaction | 标准 Input System/XRI 桥接、捏合迟滞与取消、圆圈/方块/UI、官方手势 prefab 与形状资源 |
| Android 原生桥接 | Android 仓库 SpatialHandBridge.java、README.md | 默认 CPU、运行中同线程切换 GPU、回退与原始手别置信度 |
| 显示与构建 | 基础包 SpatialDisplayScene/SpatialDisplaySample、SpatialAndroidSetup、SpatialBuildCheck、SpatialHandGradleDependencies | 最小双目/3DOF 示例、Android XR 配置及消费者 APK 构建 |
| SDK 安装/打包 | 基础包 Editor/PackageInstallation、package.json、Pack.ps1、PackUnityRelease.py、VerifyAndroidHandAar.ps1、Android 源码锁 | 基础 tgz 内安装三包、校验/恢复/升级、确定性打包和源码制品对应 |
| 现有 Demo 适配 | samples/unity/SpatialXR 的场景、脚本、输入资产与 prefab；基础包 HelloSpatialXR Sample | 使用 SDK 统一补偿，避免重复实现，保留场景切换与推理后端调试 |
| 文档 | 阶段 0～5、总体计划、SDK_INSTALL、第三方说明、补偿/诊断/光学和验证记录 | 安装交付、接口限制、后续工作和复验依据 |

全部 Unity `.meta` 随对应源码、文件夹、场景和资源保留，避免导入后 GUID/序列化引用丢失。场景、prefab、inputactions 与手势形状 asset 是交付资源，不按构建产物排除。

## 验证工具去留

验证程序位于 tests/tools，不随运行时进入 Android APK。分阶段名称保留用于定位历史结果，不意味着工具只运行一次。现有阶段 5 只覆盖发布消费主流程，不能替代阶段 0～4 的深入回归。

| 文件/组 | 结论 | 保留理由 |
| --- | --- | --- |
| tools/ValidateXRHandsStage0.ps1 + tests/XRHandsStage0 | 保留 | 基础源码包不强制 XR Hands，固定版本依赖隔离、26 点/21 点映射与能力边界 |
| tools/ValidateXRHandsStage1.ps1 + tests/XRHandsStage1 | 保留 | 有效关节与缺失数据、刚体变换、标准回调、启停/Play 往返及场景重载无重复子系统 |
| tools/ValidateXRHandsStage2.ps1 + tests/XRHandsStage2 | 保留 | Unity JSON/姿态桥接、标定缺省/覆盖、相机尺寸和坐标错误拒绝；不只是 Core 测试重复 |
| tools/ValidateXRHandsStage3.ps1 + tests/XRHandsStage3 | 保留 | 真实帧源的合成时序回归、超时/停用/恢复、共享补偿、显示保留与有效输入的分离 |
| tools/ValidateXRHandsStage4.ps1 + tests/XRHandsStage4 | 保留 | 捏合迟滞、丢失取消、恢复武装、左右手独立点击、标准手势事件与资源/设备生命周期 |
| tools/ValidateXRHandsStage5.ps1 + tools/PrepareXRHandsStage5.py + tests/XRHandsStage5 的验证类 | 保留 | 从实际发布 tgz 验证消费工程、纯显示不启动手部、旧场景升级、示例引用与 APK 构建 |
| tools/ValidateUnityInstaller.ps1 + tools/PrepareUnityInstallerValidation.py + tests/UnityInstaller | 保留 | 只导入主包、三包内置内容校验、重载/重试、下载路径独立、旧 installer 移除及工程搬迁 |
| tests/CoreChecks/HandContractChecks.cs、UvcGeometryChecks.cs、项目入口改动 | 保留 | 无 Unity 运行环境即可验证契约、投影/反投影、映射、估算和退化输入 |
| tests/UnitySmoke/UvcHandDemoCheck.cs | 保留 | 旧 hands 场景使用 SDK 共享补偿后的身份、侧掌、镜像、短暂丢失和标定覆盖回归 |
| tools/CalibrateUvcCamera.py、uvc-calibration-requirements.txt、tests/UvcCalibration | 保留 | 用户要求标定暂缓但工具保留；内参、刚体外参、误差报告及合成/实机证据区分 |
| tests/XRHandsStage5/Stage5Authoring.cs | 保留 | 重新导出包内示例并保持已有 GUID，供 SDK 资源维护使用；它是资源生成器，不是一次性验证 |

Stage5Authoring 的复用方式：在安装全部 SDK 配套包的新隔离 Unity 工程中，将该脚本放入 Assets/Editor，设置 `SPATIALAI_SAMPLE_EXPORT_ROOT` 为 SDK 仓库 packages 的绝对路径，使用 Unity `-executeMethod Stage5Authoring.Export` 导出；检查场景/prefab/形状变更后再打包。应使用新 authoring 工程，避免已有同名场景被 Ensure 的保留逻辑复用。它会更新源码中的样例资源，不能作为用户安装步骤运行。

审核中发现阶段 5 旧版本验证原先使用可变 HEAD，后续提交会使基线失效。本次已固定为真实 preview.9 提交 `4cbde6bac79762a55522aafd702fa1977797bcaf`，并提供 --baseline-revision 显式替换。即使提交当前 SDK，仍能验证原来的序列化升级路径。

本次整理验证：Core 982 项通过；标定工具 4 项合成测试通过（沙箱临时目录权限失败后，在沙箱外重跑通过）；PowerShell 语法检查和固定基线的阶段 5 工程准备通过。本次未再次执行整套 Unity 实机/长时间验收，沿用阶段文档中的结果与最新用户确认。

## 不加入 Git 但仍保留

- dist：用户当前已验证的发布 tgz/ZIP、发布清单和说明，保留用于交付；源代码只跟踪打包流程。
- 生成的 spatial-hand-unity.aar、Cardboard 原生 .so/AAR：按原约定忽略，构建制品进入发行包；源码锁、补丁与 importer metadata 保留。
- third_party/checkouts、build/native、工具依赖与必要缓存：本地构建/打包依赖，本轮不清除。
- build/fixtures/legacy-installer.tgz：旧入口升级回归输入，暂保留，不跟踪；未来可用保存的旧发行包替代。
- build/validation 的日志、报告、APK 与场景证据：暂保留，不跟踪；需要清理空间时应先归档日志，Unity Library/Temp 等缓存可以重新生成。
- build/prior-conversation-text.txt 与工作区原会话 HTML：原始资料暂保留，不加入本轮代码提交。
- 用户 Demo 工程的备份及临时导入记录：在 SDK 仓库外，本轮不删除、不暂存。

## 用户已确认删除

2026-10-12 用户确认后，以下 21 个文件已全部删除。它们均位于 SDK 仓库 build 下，原本被 .gitignore 排除、未加入暂存区。删除不影响已验证的发行包、保留的回归工具或两个仓库的暂存文件范围；验证脚本下次运行会重新生成需要的运行路径标记。

| 文件 | 删除理由 |
| --- | --- |
| `D:/visor-x/spatial-ai-sdk/build/check-installer-release.py` | 旧独立 installer 的一次性制品检查，当前 PrepareUnityInstallerValidation 已覆盖 |
| `D:/visor-x/spatial-ai-sdk/build/merge_sdk_installer.py` | 已执行的包结构迁移脚本，不能作为后续构建步骤重跑 |
| `D:/visor-x/spatial-ai-sdk/build/refactor_uvc_demo.py` | 已执行的旧场景源码改写，正式源码已保留 |
| `D:/visor-x/spatial-ai-sdk/build/update_install_docs.py` | 已执行的文档批量改写，当前正式文档已保留 |
| `D:/visor-x/spatial-ai-sdk/build/verify_merged_release.py` | 针对单次交付哈希的检查，正式校验入口已保留 |
| `D:/visor-x/spatial-ai-sdk/build/ConsumerProjectSetup.cs` | 用户 Demo 的临时配置脚本，SDK 菜单已提供正式功能 |
| `D:/visor-x/spatial-ai-sdk/build/ConsumerProjectCleanup.cs` | 用户 Demo 的一次性清理脚本，不属于 SDK 接入工具 |
| `D:/visor-x/spatial-ai-sdk/build/ConsumerProjectPlayCheck.cs` | 指定 Demo 场景的临时检查，Stage5/Stage4 回归覆盖标准接口 |
| `D:/visor-x/spatial-ai-sdk/build/ConsumerCleanImportGuide.md` | 旧手动四包安装的临时说明，SDK_INSTALL 已替代 |
| `D:/visor-x/spatial-ai-sdk/build/ConsumerQuickstart.md` | 旧源码导入 Demo 说明，当前正式安装/示例文档已替代 |
| `D:/visor-x/spatial-ai-sdk/build/embedded-release-before.json` | 已废弃 Embedded 安装方案的临时快照 |
| `D:/visor-x/spatial-ai-sdk/build/installer-release-before-checksums.json` | 独立 installer 迭代前的临时校验快照 |
| `D:/visor-x/spatial-ai-sdk/build/embedded-validation-path.txt` | 旧/本次运行的路径或 PID 标记，可重新生成 |
| `D:/visor-x/spatial-ai-sdk/build/embedded-validation-pid.txt` | 旧/本次运行的路径或 PID 标记，可重新生成 |
| `D:/visor-x/spatial-ai-sdk/build/install-four-packages-validation-path.txt` | 旧/本次运行的路径或 PID 标记，可重新生成 |
| `D:/visor-x/spatial-ai-sdk/build/install-four-packages-validation-pid.txt` | 旧/本次运行的路径或 PID 标记，可重新生成 |
| `D:/visor-x/spatial-ai-sdk/build/installer-relocation-pid.txt` | 旧/本次运行的路径或 PID 标记，可重新生成 |
| `D:/visor-x/spatial-ai-sdk/build/installer-validation-path.txt` | 旧/本次运行的路径或 PID 标记，可重新生成 |
| `D:/visor-x/spatial-ai-sdk/build/installer-validation-pid.txt` | 旧/本次运行的路径或 PID 标记，可重新生成 |
| `D:/visor-x/spatial-ai-sdk/build/stage5-authoring-path.txt` | 旧/本次运行的路径或 PID 标记，可重新生成 |
| `D:/visor-x/spatial-ai-sdk/build/stage5-validation-path.txt` | 旧/本次运行的路径或 PID 标记，可重新生成 |

## 后续提交顺序

当前只准备暂存区。后续创建 commit 时，Android CPU/GPU 和置信度源码改动应与 SDK 侧改动一并提交到各自仓库，避免只交付 Unity 消费端。
先提交 Android 仓库，再将 SDK 的 third_party/spatial-ai-android.lock.json revision 更新为该新提交并去除 local-working-tree 状态；核对 sourceFiles 与 AAR/模型哈希后提交 SDK。已验证 AAR 不跟踪；源码未变时无需仅为改锁而重编译。重新打包才能让发行清单使用已提交版本。本轮暂存保留目前真实来源记录，不提前伪造未存在的提交号。

## 保留文件完整清单

下面以本次暂存前的工作区变化生成：A 为新增，M 为修改。包括 `.meta`；以前已跟踪且未变化的文件不重复列出。

### spatial-ai-sdk：212 个文件（新增 185，修改 27；其中 .meta 76）

| 状态 | 文件 |
| --- | --- |
| M | [README.md](D:/visor-x/spatial-ai-sdk/README.md) |
| A | [docs/SDK_INSTALL.md](D:/visor-x/spatial-ai-sdk/docs/SDK_INSTALL.md) |
| A | [docs/THIRD_PARTY_NOTICES.md](D:/visor-x/spatial-ai-sdk/docs/THIRD_PARTY_NOTICES.md) |
| A | [docs/UNITY_XR_HANDS_CHANGE_REVIEW.md](D:/visor-x/spatial-ai-sdk/docs/UNITY_XR_HANDS_CHANGE_REVIEW.md) |
| A | [docs/UNITY_XR_HANDS_IMPLEMENTATION_PLAN.md](D:/visor-x/spatial-ai-sdk/docs/UNITY_XR_HANDS_IMPLEMENTATION_PLAN.md) |
| A | [docs/UNITY_XR_HANDS_STAGE0.md](D:/visor-x/spatial-ai-sdk/docs/UNITY_XR_HANDS_STAGE0.md) |
| A | [docs/UNITY_XR_HANDS_STAGE1.md](D:/visor-x/spatial-ai-sdk/docs/UNITY_XR_HANDS_STAGE1.md) |
| A | [docs/UNITY_XR_HANDS_STAGE2.md](D:/visor-x/spatial-ai-sdk/docs/UNITY_XR_HANDS_STAGE2.md) |
| A | [docs/UNITY_XR_HANDS_STAGE3.md](D:/visor-x/spatial-ai-sdk/docs/UNITY_XR_HANDS_STAGE3.md) |
| A | [docs/UNITY_XR_HANDS_STAGE4.md](D:/visor-x/spatial-ai-sdk/docs/UNITY_XR_HANDS_STAGE4.md) |
| A | [docs/UNITY_XR_HANDS_STAGE5.md](D:/visor-x/spatial-ai-sdk/docs/UNITY_XR_HANDS_STAGE5.md) |
| A | [docs/UVC_HAND_DIAGNOSTICS.md](D:/visor-x/spatial-ai-sdk/docs/UVC_HAND_DIAGNOSTICS.md) |
| A | [docs/UVC_HAND_SDK_COMPENSATION.md](D:/visor-x/spatial-ai-sdk/docs/UVC_HAND_SDK_COMPENSATION.md) |
| M | [docs/UVC_HAND_TRACKING_PROGRESS.md](D:/visor-x/spatial-ai-sdk/docs/UVC_HAND_TRACKING_PROGRESS.md) |
| M | [docs/VALIDATION.md](D:/visor-x/spatial-ai-sdk/docs/VALIDATION.md) |
| M | [docs/XIAOMI_OPTICS_SCALE_VALIDATION.md](D:/visor-x/spatial-ai-sdk/docs/XIAOMI_OPTICS_SCALE_VALIDATION.md) |
| A | [docs/calibration/hxq-board-9x6-20mm.svg](D:/visor-x/spatial-ai-sdk/docs/calibration/hxq-board-9x6-20mm.svg) |
| A | [docs/calibration/hxq-calibration-active.json](D:/visor-x/spatial-ai-sdk/docs/calibration/hxq-calibration-active.json)（2026-10-15 合并为当前有效配置） |
| — | 外参点对模板现内嵌于 [阶段 2 标定流程](D:/visor-x/spatial-ai-sdk/docs/UNITY_XR_HANDS_STAGE2.md)，不再另存 HXQ JSON 模板 |
| A | [docs/calibration/hxq-hand-accuracy.template.csv](D:/visor-x/spatial-ai-sdk/docs/calibration/hxq-hand-accuracy.template.csv) |
| M | [packages/com.spatialai.sdk/CHANGELOG.md](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/CHANGELOG.md) |
| A | [packages/com.spatialai.sdk/Documentation~.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Documentation~.meta) |
| A | [packages/com.spatialai.sdk/Documentation~/INSTALL.md](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Documentation~/INSTALL.md) |
| A | [packages/com.spatialai.sdk/Documentation~/INSTALL.md.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Documentation~/INSTALL.md.meta) |
| A | [packages/com.spatialai.sdk/Documentation~/UVC_HAND_SDK_COMPENSATION.md](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Documentation~/UVC_HAND_SDK_COMPENSATION.md) |
| A | [packages/com.spatialai.sdk/Documentation~/UVC_HAND_SDK_COMPENSATION.md.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Documentation~/UVC_HAND_SDK_COMPENSATION.md.meta) |
| A | [packages/com.spatialai.sdk/Editor/PackageInstallation.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Editor/PackageInstallation.meta) |
| A | [packages/com.spatialai.sdk/Editor/PackageInstallation/SpatialAI.SDKInstallation.Editor.asmdef](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Editor/PackageInstallation/SpatialAI.SDKInstallation.Editor.asmdef) |
| A | [packages/com.spatialai.sdk/Editor/PackageInstallation/SpatialAI.SDKInstallation.Editor.asmdef.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Editor/PackageInstallation/SpatialAI.SDKInstallation.Editor.asmdef.meta) |
| A | [packages/com.spatialai.sdk/Editor/PackageInstallation/SpatialPackageInstaller.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Editor/PackageInstallation/SpatialPackageInstaller.cs) |
| A | [packages/com.spatialai.sdk/Editor/PackageInstallation/SpatialPackageInstaller.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Editor/PackageInstallation/SpatialPackageInstaller.cs.meta) |
| M | [packages/com.spatialai.sdk/Editor/SpatialAI.Editor.asmdef](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Editor/SpatialAI.Editor.asmdef) |
| A | [packages/com.spatialai.sdk/Editor/SpatialAndroidSetup.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Editor/SpatialAndroidSetup.cs) |
| A | [packages/com.spatialai.sdk/Editor/SpatialAndroidSetup.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Editor/SpatialAndroidSetup.cs.meta) |
| M | [packages/com.spatialai.sdk/Editor/SpatialBuildCheck.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Editor/SpatialBuildCheck.cs) |
| A | [packages/com.spatialai.sdk/Editor/SpatialDisplayScene.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Editor/SpatialDisplayScene.cs) |
| A | [packages/com.spatialai.sdk/Editor/SpatialDisplayScene.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Editor/SpatialDisplayScene.cs.meta) |
| M | [packages/com.spatialai.sdk/Editor/SpatialHandGradleDependencies.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Editor/SpatialHandGradleDependencies.cs) |
| A | [packages/com.spatialai.sdk/Editor/SpatialUvcCalibrationScene.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Editor/SpatialUvcCalibrationScene.cs) |
| A | [packages/com.spatialai.sdk/Editor/SpatialUvcCalibrationScene.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Editor/SpatialUvcCalibrationScene.cs.meta) |
| M | [packages/com.spatialai.sdk/README.md](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/README.md) |
| A | [packages/com.spatialai.sdk/Runtime/Core/HandContracts.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Core/HandContracts.cs) |
| A | [packages/com.spatialai.sdk/Runtime/Core/HandContracts.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Core/HandContracts.cs.meta) |
| M | [packages/com.spatialai.sdk/Runtime/Core/HostDeviceProfiles.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Core/HostDeviceProfiles.cs) |
| A | [packages/com.spatialai.sdk/Runtime/Core/MonocularHandConverter.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Core/MonocularHandConverter.cs) |
| A | [packages/com.spatialai.sdk/Runtime/Core/MonocularHandConverter.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Core/MonocularHandConverter.cs.meta) |
| A | [packages/com.spatialai.sdk/Runtime/Core/UvcHandGeometry.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Core/UvcHandGeometry.cs) |
| A | [packages/com.spatialai.sdk/Runtime/Core/UvcHandGeometry.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Core/UvcHandGeometry.cs.meta) |
| M | [packages/com.spatialai.sdk/Runtime/Unity/Resources/SpatialAI/HostProfiles/xiaomi-mayfly.json](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Unity/Resources/SpatialAI/HostProfiles/xiaomi-mayfly.json) |
| A | [packages/com.spatialai.sdk/Runtime/Unity/SpatialDisplaySample.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Unity/SpatialDisplaySample.cs) |
| A | [packages/com.spatialai.sdk/Runtime/Unity/SpatialDisplaySample.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Unity/SpatialDisplaySample.cs.meta) |
| M | [packages/com.spatialai.sdk/Runtime/Unity/SpatialHandTracking.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Unity/SpatialHandTracking.cs) |
| A | [packages/com.spatialai.sdk/Runtime/Unity/SpatialUvcCalibrationRecorder.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Unity/SpatialUvcCalibrationRecorder.cs) |
| A | [packages/com.spatialai.sdk/Runtime/Unity/SpatialUvcCalibrationRecorder.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Unity/SpatialUvcCalibrationRecorder.cs.meta) |
| A | [packages/com.spatialai.sdk/Runtime/Unity/SpatialUvcHandProcessor.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Unity/SpatialUvcHandProcessor.cs) |
| A | [packages/com.spatialai.sdk/Runtime/Unity/SpatialUvcHandProcessor.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Unity/SpatialUvcHandProcessor.cs.meta) |
| A | [packages/com.spatialai.sdk/Runtime/Unity/SpatialUvcHandStabilizer.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Unity/SpatialUvcHandStabilizer.cs) |
| A | [packages/com.spatialai.sdk/Runtime/Unity/SpatialUvcHandStabilizer.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Unity/SpatialUvcHandStabilizer.cs.meta) |
| A | [packages/com.spatialai.sdk/Runtime/Unity/SpatialUvcHandsConverter.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Unity/SpatialUvcHandsConverter.cs) |
| A | [packages/com.spatialai.sdk/Runtime/Unity/SpatialUvcHandsConverter.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Runtime/Unity/SpatialUvcHandsConverter.cs.meta) |
| A | [packages/com.spatialai.sdk/Samples~/Display3DOF.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Samples~/Display3DOF.meta) |
| A | [packages/com.spatialai.sdk/Samples~/Display3DOF/README.md](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Samples~/Display3DOF/README.md) |
| A | [packages/com.spatialai.sdk/Samples~/Display3DOF/README.md.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Samples~/Display3DOF/README.md.meta) |
| A | [packages/com.spatialai.sdk/Samples~/Display3DOF/SpatialDisplay.unity](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Samples~/Display3DOF/SpatialDisplay.unity) |
| A | [packages/com.spatialai.sdk/Samples~/Display3DOF/SpatialDisplay.unity.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Samples~/Display3DOF/SpatialDisplay.unity.meta) |
| M | [packages/com.spatialai.sdk/Samples~/HelloSpatialXR/HandTrackingDemo.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Samples~/HelloSpatialXR/HandTrackingDemo.cs) |
| M | [packages/com.spatialai.sdk/Samples~/HelloSpatialXR/HelloSpatialXR.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/Samples~/HelloSpatialXR/HelloSpatialXR.cs) |
| M | [packages/com.spatialai.sdk/package.json](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk/package.json) |
| A | [packages/com.spatialai.xr-hands/Editor.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Editor.meta) |
| A | [packages/com.spatialai.xr-hands/Editor/SpatialAI.XRHands.Editor.asmdef](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Editor/SpatialAI.XRHands.Editor.asmdef) |
| A | [packages/com.spatialai.xr-hands/Editor/SpatialAI.XRHands.Editor.asmdef.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Editor/SpatialAI.XRHands.Editor.asmdef.meta) |
| A | [packages/com.spatialai.xr-hands/Editor/SpatialHandsSimulationScene.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Editor/SpatialHandsSimulationScene.cs) |
| A | [packages/com.spatialai.xr-hands/Editor/SpatialHandsSimulationScene.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Editor/SpatialHandsSimulationScene.cs.meta) |
| A | [packages/com.spatialai.xr-hands/Editor/SpatialUvcHandsScene.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Editor/SpatialUvcHandsScene.cs) |
| A | [packages/com.spatialai.xr-hands/Editor/SpatialUvcHandsScene.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Editor/SpatialUvcHandsScene.cs.meta) |
| A | [packages/com.spatialai.xr-hands/README.md](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/README.md) |
| A | [packages/com.spatialai.xr-hands/README.md.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/README.md.meta) |
| A | [packages/com.spatialai.xr-hands/Runtime.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime.meta) |
| A | [packages/com.spatialai.xr-hands/Runtime/SimulatedHandsFrameSource.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SimulatedHandsFrameSource.cs) |
| A | [packages/com.spatialai.xr-hands/Runtime/SimulatedHandsFrameSource.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SimulatedHandsFrameSource.cs.meta) |
| A | [packages/com.spatialai.xr-hands/Runtime/SpatialAI.XRHands.asmdef](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SpatialAI.XRHands.asmdef) |
| A | [packages/com.spatialai.xr-hands/Runtime/SpatialAI.XRHands.asmdef.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SpatialAI.XRHands.asmdef.meta) |
| A | [packages/com.spatialai.xr-hands/Runtime/SpatialHandSubsystem.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SpatialHandSubsystem.cs) |
| A | [packages/com.spatialai.xr-hands/Runtime/SpatialHandSubsystem.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SpatialHandSubsystem.cs.meta) |
| A | [packages/com.spatialai.xr-hands/Runtime/SpatialHandsSimulationControls.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SpatialHandsSimulationControls.cs) |
| A | [packages/com.spatialai.xr-hands/Runtime/SpatialHandsSimulationControls.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SpatialHandsSimulationControls.cs.meta) |
| A | [packages/com.spatialai.xr-hands/Runtime/SpatialHandsSimulator.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SpatialHandsSimulator.cs) |
| A | [packages/com.spatialai.xr-hands/Runtime/SpatialHandsSimulator.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SpatialHandsSimulator.cs.meta) |
| A | [packages/com.spatialai.xr-hands/Runtime/SpatialUvcHands.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SpatialUvcHands.cs) |
| A | [packages/com.spatialai.xr-hands/Runtime/SpatialUvcHands.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SpatialUvcHands.cs.meta) |
| A | [packages/com.spatialai.xr-hands/Runtime/SpatialUvcHandsSample.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SpatialUvcHandsSample.cs) |
| A | [packages/com.spatialai.xr-hands/Runtime/SpatialUvcHandsSample.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SpatialUvcHandsSample.cs.meta) |
| A | [packages/com.spatialai.xr-hands/Runtime/SpatialXRHandsContract.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SpatialXRHandsContract.cs) |
| A | [packages/com.spatialai.xr-hands/Runtime/SpatialXRHandsContract.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SpatialXRHandsContract.cs.meta) |
| A | [packages/com.spatialai.xr-hands/Runtime/SpatialXRHandsVisualizer.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SpatialXRHandsVisualizer.cs) |
| A | [packages/com.spatialai.xr-hands/Runtime/SpatialXRHandsVisualizer.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/SpatialXRHandsVisualizer.cs.meta) |
| A | [packages/com.spatialai.xr-hands/Runtime/UvcHandsFrameSource.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/UvcHandsFrameSource.cs) |
| A | [packages/com.spatialai.xr-hands/Runtime/UvcHandsFrameSource.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Runtime/UvcHandsFrameSource.cs.meta) |
| A | [packages/com.spatialai.xr-hands/Samples~.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Samples~.meta) |
| A | [packages/com.spatialai.xr-hands/Samples~/HandsSimulation.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Samples~/HandsSimulation.meta) |
| A | [packages/com.spatialai.xr-hands/Samples~/HandsSimulation/README.md](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Samples~/HandsSimulation/README.md) |
| A | [packages/com.spatialai.xr-hands/Samples~/HandsSimulation/README.md.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Samples~/HandsSimulation/README.md.meta) |
| A | [packages/com.spatialai.xr-hands/Samples~/HandsSimulation/SpatialHandsSimulation.unity](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Samples~/HandsSimulation/SpatialHandsSimulation.unity) |
| A | [packages/com.spatialai.xr-hands/Samples~/HandsSimulation/SpatialHandsSimulation.unity.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Samples~/HandsSimulation/SpatialHandsSimulation.unity.meta) |
| A | [packages/com.spatialai.xr-hands/Samples~/HandsUvc.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Samples~/HandsUvc.meta) |
| A | [packages/com.spatialai.xr-hands/Samples~/HandsUvc/README.md](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Samples~/HandsUvc/README.md) |
| A | [packages/com.spatialai.xr-hands/Samples~/HandsUvc/README.md.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Samples~/HandsUvc/README.md.meta) |
| A | [packages/com.spatialai.xr-hands/Samples~/HandsUvc/SpatialUvcXRHands.unity](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Samples~/HandsUvc/SpatialUvcXRHands.unity) |
| A | [packages/com.spatialai.xr-hands/Samples~/HandsUvc/SpatialUvcXRHands.unity.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/Samples~/HandsUvc/SpatialUvcXRHands.unity.meta) |
| A | [packages/com.spatialai.xr-hands/package.json](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/package.json) |
| A | [packages/com.spatialai.xr-hands/package.json.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands/package.json.meta) |
| A | [packages/com.spatialai.xr-interaction/Editor.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Editor.meta) |
| A | [packages/com.spatialai.xr-interaction/Editor/SpatialAI.XRInteraction.Editor.asmdef](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Editor/SpatialAI.XRInteraction.Editor.asmdef) |
| A | [packages/com.spatialai.xr-interaction/Editor/SpatialAI.XRInteraction.Editor.asmdef.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Editor/SpatialAI.XRInteraction.Editor.asmdef.meta) |
| A | [packages/com.spatialai.xr-interaction/Editor/SpatialHandInteractionScene.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Editor/SpatialHandInteractionScene.cs) |
| A | [packages/com.spatialai.xr-interaction/Editor/SpatialHandInteractionScene.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Editor/SpatialHandInteractionScene.cs.meta) |
| A | [packages/com.spatialai.xr-interaction/README.md](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/README.md) |
| A | [packages/com.spatialai.xr-interaction/README.md.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/README.md.meta) |
| A | [packages/com.spatialai.xr-interaction/Runtime.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Runtime.meta) |
| A | [packages/com.spatialai.xr-interaction/Runtime/SpatialAI.XRInteraction.asmdef](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Runtime/SpatialAI.XRInteraction.asmdef) |
| A | [packages/com.spatialai.xr-interaction/Runtime/SpatialAI.XRInteraction.asmdef.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Runtime/SpatialAI.XRInteraction.asmdef.meta) |
| A | [packages/com.spatialai.xr-interaction/Runtime/SpatialHandInputDevice.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Runtime/SpatialHandInputDevice.cs) |
| A | [packages/com.spatialai.xr-interaction/Runtime/SpatialHandInputDevice.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Runtime/SpatialHandInputDevice.cs.meta) |
| A | [packages/com.spatialai.xr-interaction/Runtime/SpatialHandInteraction.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Runtime/SpatialHandInteraction.cs) |
| A | [packages/com.spatialai.xr-interaction/Runtime/SpatialHandInteraction.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Runtime/SpatialHandInteraction.cs.meta) |
| A | [packages/com.spatialai.xr-interaction/Runtime/SpatialHandInteractionRig.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Runtime/SpatialHandInteractionRig.cs) |
| A | [packages/com.spatialai.xr-interaction/Runtime/SpatialHandInteractionRig.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Runtime/SpatialHandInteractionRig.cs.meta) |
| A | [packages/com.spatialai.xr-interaction/Runtime/SpatialHandInteractionSample.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Runtime/SpatialHandInteractionSample.cs) |
| A | [packages/com.spatialai.xr-interaction/Runtime/SpatialHandInteractionSample.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Runtime/SpatialHandInteractionSample.cs.meta) |
| A | [packages/com.spatialai.xr-interaction/Runtime/SpatialHandRayBinding.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Runtime/SpatialHandRayBinding.cs) |
| A | [packages/com.spatialai.xr-interaction/Runtime/SpatialHandRayBinding.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Runtime/SpatialHandRayBinding.cs.meta) |
| A | [packages/com.spatialai.xr-interaction/Runtime/SpatialInteractionTarget.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Runtime/SpatialInteractionTarget.cs) |
| A | [packages/com.spatialai.xr-interaction/Runtime/SpatialInteractionTarget.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Runtime/SpatialInteractionTarget.cs.meta) |
| A | [packages/com.spatialai.xr-interaction/Runtime/SpatialPinchGate.cs](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Runtime/SpatialPinchGate.cs) |
| A | [packages/com.spatialai.xr-interaction/Runtime/SpatialPinchGate.cs.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Runtime/SpatialPinchGate.cs.meta) |
| A | [packages/com.spatialai.xr-interaction/Samples~.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~.meta) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction.meta) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/Gestures.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/Gestures.meta) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/Gestures/FIST.asset](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/Gestures/FIST.asset) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/Gestures/FIST.asset.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/Gestures/FIST.asset.meta) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/Gestures/OPEN.asset](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/Gestures/OPEN.asset) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/Gestures/OPEN.asset.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/Gestures/OPEN.asset.meta) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/Gestures/PINCH.asset](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/Gestures/PINCH.asset) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/Gestures/PINCH.asset.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/Gestures/PINCH.asset.meta) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/README.md](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/README.md) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/README.md.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/README.md.meta) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/SpatialHandActions.inputactions](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/SpatialHandActions.inputactions) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/SpatialHandActions.inputactions.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/SpatialHandActions.inputactions.meta) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/SpatialHandInteraction.unity](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/SpatialHandInteraction.unity) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/SpatialHandInteraction.unity.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/SpatialHandInteraction.unity.meta) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/SpatialHandInteractionRig.prefab](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/SpatialHandInteractionRig.prefab) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/SpatialHandInteractionRig.prefab.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/SpatialHandInteractionRig.prefab.meta) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/XRHandsStandardGestures.prefab](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/XRHandsStandardGestures.prefab) |
| A | [packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/XRHandsStandardGestures.prefab.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/Samples~/HandsInteraction/XRHandsStandardGestures.prefab.meta) |
| A | [packages/com.spatialai.xr-interaction/package.json](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/package.json) |
| A | [packages/com.spatialai.xr-interaction/package.json.meta](D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-interaction/package.json.meta) |
| M | [samples/unity/SpatialXR/Assets/Editor/SpatialXRSetup.cs](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/Assets/Editor/SpatialXRSetup.cs) |
| M | [samples/unity/SpatialXR/Assets/Plugins/Android/SpatialXRSceneControl.java](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/Assets/Plugins/Android/SpatialXRSceneControl.java) |
| A | [samples/unity/SpatialXR/Assets/Scenes/SpatialHandInteraction.unity](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/Assets/Scenes/SpatialHandInteraction.unity) |
| A | [samples/unity/SpatialXR/Assets/Scenes/SpatialHandInteraction.unity.meta](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/Assets/Scenes/SpatialHandInteraction.unity.meta) |
| A | [samples/unity/SpatialXR/Assets/Scenes/SpatialUvcXRHands.unity](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/Assets/Scenes/SpatialUvcXRHands.unity) |
| A | [samples/unity/SpatialXR/Assets/Scenes/SpatialUvcXRHands.unity.meta](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/Assets/Scenes/SpatialUvcXRHands.unity.meta) |
| A | [samples/unity/SpatialXR/Assets/SpatialAI.meta](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/Assets/SpatialAI.meta) |
| A | [samples/unity/SpatialXR/Assets/SpatialAI/Input.meta](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/Assets/SpatialAI/Input.meta) |
| A | [samples/unity/SpatialXR/Assets/SpatialAI/Input/SpatialHandActions.inputactions](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/Assets/SpatialAI/Input/SpatialHandActions.inputactions) |
| A | [samples/unity/SpatialXR/Assets/SpatialAI/Input/SpatialHandActions.inputactions.meta](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/Assets/SpatialAI/Input/SpatialHandActions.inputactions.meta) |
| A | [samples/unity/SpatialXR/Assets/SpatialAI/Prefabs.meta](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/Assets/SpatialAI/Prefabs.meta) |
| A | [samples/unity/SpatialXR/Assets/SpatialAI/Prefabs/SpatialHandInteractionRig.prefab](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/Assets/SpatialAI/Prefabs/SpatialHandInteractionRig.prefab) |
| A | [samples/unity/SpatialXR/Assets/SpatialAI/Prefabs/SpatialHandInteractionRig.prefab.meta](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/Assets/SpatialAI/Prefabs/SpatialHandInteractionRig.prefab.meta) |
| M | [samples/unity/SpatialXR/Assets/SpatialXR/HandTrackingDemo.cs](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/Assets/SpatialXR/HandTrackingDemo.cs) |
| M | [samples/unity/SpatialXR/Assets/SpatialXR/HelloSpatialXR.cs](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/Assets/SpatialXR/HelloSpatialXR.cs) |
| M | [samples/unity/SpatialXR/Assets/SpatialXR/SpatialXRSceneControl.cs](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/Assets/SpatialXR/SpatialXRSceneControl.cs) |
| M | [samples/unity/SpatialXR/Packages/manifest.json](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/Packages/manifest.json) |
| M | [samples/unity/SpatialXR/README.md](D:/visor-x/spatial-ai-sdk/samples/unity/SpatialXR/README.md) |
| M | [tests/CoreChecks/CoreChecks.csproj](D:/visor-x/spatial-ai-sdk/tests/CoreChecks/CoreChecks.csproj) |
| A | [tests/CoreChecks/HandContractChecks.cs](D:/visor-x/spatial-ai-sdk/tests/CoreChecks/HandContractChecks.cs) |
| M | [tests/CoreChecks/Program.cs](D:/visor-x/spatial-ai-sdk/tests/CoreChecks/Program.cs) |
| A | [tests/CoreChecks/UvcGeometryChecks.cs](D:/visor-x/spatial-ai-sdk/tests/CoreChecks/UvcGeometryChecks.cs) |
| A | [tests/UnityInstaller/InstallerVerification.cs](D:/visor-x/spatial-ai-sdk/tests/UnityInstaller/InstallerVerification.cs) |
| A | [tests/UnitySmoke/UvcHandDemoCheck.cs](D:/visor-x/spatial-ai-sdk/tests/UnitySmoke/UvcHandDemoCheck.cs) |
| A | [tests/UvcCalibration/test_calibration_tools.py](D:/visor-x/spatial-ai-sdk/tests/UvcCalibration/test_calibration_tools.py) |
| A | [tests/XRHandsStage0/Stage0CompileProbe.cs](D:/visor-x/spatial-ai-sdk/tests/XRHandsStage0/Stage0CompileProbe.cs) |
| A | [tests/XRHandsStage1/Stage1LoopProbe.cs](D:/visor-x/spatial-ai-sdk/tests/XRHandsStage1/Stage1LoopProbe.cs) |
| A | [tests/XRHandsStage1/Stage1PlayProbe.cs](D:/visor-x/spatial-ai-sdk/tests/XRHandsStage1/Stage1PlayProbe.cs) |
| A | [tests/XRHandsStage1/Stage1SceneProbe.cs](D:/visor-x/spatial-ai-sdk/tests/XRHandsStage1/Stage1SceneProbe.cs) |
| A | [tests/XRHandsStage1/Stage1Verification.cs](D:/visor-x/spatial-ai-sdk/tests/XRHandsStage1/Stage1Verification.cs) |
| A | [tests/XRHandsStage2/Stage2Verification.cs](D:/visor-x/spatial-ai-sdk/tests/XRHandsStage2/Stage2Verification.cs) |
| A | [tests/XRHandsStage3/Stage3PlayProbe.cs](D:/visor-x/spatial-ai-sdk/tests/XRHandsStage3/Stage3PlayProbe.cs) |
| A | [tests/XRHandsStage3/Stage3Verification.cs](D:/visor-x/spatial-ai-sdk/tests/XRHandsStage3/Stage3Verification.cs) |
| A | [tests/XRHandsStage4/Stage4PlayProbe.cs](D:/visor-x/spatial-ai-sdk/tests/XRHandsStage4/Stage4PlayProbe.cs) |
| A | [tests/XRHandsStage4/Stage4Verification.cs](D:/visor-x/spatial-ai-sdk/tests/XRHandsStage4/Stage4Verification.cs) |
| A | [tests/XRHandsStage5/Stage5Authoring.cs](D:/visor-x/spatial-ai-sdk/tests/XRHandsStage5/Stage5Authoring.cs) |
| A | [tests/XRHandsStage5/Stage5DisplayVerification.cs](D:/visor-x/spatial-ai-sdk/tests/XRHandsStage5/Stage5DisplayVerification.cs) |
| A | [tests/XRHandsStage5/Stage5PlayProbe.cs](D:/visor-x/spatial-ai-sdk/tests/XRHandsStage5/Stage5PlayProbe.cs) |
| A | [tests/XRHandsStage5/Stage5UpgradeBaseline.cs](D:/visor-x/spatial-ai-sdk/tests/XRHandsStage5/Stage5UpgradeBaseline.cs) |
| A | [tests/XRHandsStage5/Stage5UpgradeVerification.cs](D:/visor-x/spatial-ai-sdk/tests/XRHandsStage5/Stage5UpgradeVerification.cs) |
| A | [tests/XRHandsStage5/Stage5Verification.cs](D:/visor-x/spatial-ai-sdk/tests/XRHandsStage5/Stage5Verification.cs) |
| M | [third_party/spatial-ai-android.lock.json](D:/visor-x/spatial-ai-sdk/third_party/spatial-ai-android.lock.json) |
| A | [tools/CalibrateUvcCamera.py](D:/visor-x/spatial-ai-sdk/tools/CalibrateUvcCamera.py) |
| M | [tools/Pack.ps1](D:/visor-x/spatial-ai-sdk/tools/Pack.ps1) |
| A | [tools/PackUnityRelease.py](D:/visor-x/spatial-ai-sdk/tools/PackUnityRelease.py) |
| A | [tools/PrepareUnityInstallerValidation.py](D:/visor-x/spatial-ai-sdk/tools/PrepareUnityInstallerValidation.py) |
| A | [tools/PrepareXRHandsStage5.py](D:/visor-x/spatial-ai-sdk/tools/PrepareXRHandsStage5.py) |
| A | [tools/ValidateUnityInstaller.ps1](D:/visor-x/spatial-ai-sdk/tools/ValidateUnityInstaller.ps1) |
| A | [tools/ValidateXRHandsStage0.ps1](D:/visor-x/spatial-ai-sdk/tools/ValidateXRHandsStage0.ps1) |
| A | [tools/ValidateXRHandsStage1.ps1](D:/visor-x/spatial-ai-sdk/tools/ValidateXRHandsStage1.ps1) |
| A | [tools/ValidateXRHandsStage2.ps1](D:/visor-x/spatial-ai-sdk/tools/ValidateXRHandsStage2.ps1) |
| A | [tools/ValidateXRHandsStage3.ps1](D:/visor-x/spatial-ai-sdk/tools/ValidateXRHandsStage3.ps1) |
| A | [tools/ValidateXRHandsStage4.ps1](D:/visor-x/spatial-ai-sdk/tools/ValidateXRHandsStage4.ps1) |
| A | [tools/ValidateXRHandsStage5.ps1](D:/visor-x/spatial-ai-sdk/tools/ValidateXRHandsStage5.ps1) |
| M | [tools/VerifyAndroidHandAar.ps1](D:/visor-x/spatial-ai-sdk/tools/VerifyAndroidHandAar.ps1) |
| A | [tools/uvc-calibration-requirements.txt](D:/visor-x/spatial-ai-sdk/tools/uvc-calibration-requirements.txt) |

### spatial-ai-android：2 个文件（新增 0，修改 2；其中 .meta 0）

| 状态 | 文件 |
| --- | --- |
| M | [README.md](D:/visor-x/spatial-ai-android/README.md) |
| M | [runtime/platform/android/hands/SpatialHandBridge.java](D:/visor-x/spatial-ai-android/runtime/platform/android/hands/SpatialHandBridge.java) |

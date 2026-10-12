# Unity XR Hands 阶段 5：SDK 交付与全新工程验收

日期：2026-10-12。用户已确认阶段 4 的 CPU 默认版本、侧掌补偿、左右手/双手、捏合和丢失恢复无问题，本阶段完成可移植预览 SDK 交付流程。

## 交付范围

- 基础 SDK 0.2.0-preview.10；XR Hands / XR Interaction 0.1.0-preview.2；Spatial 修改版 Cardboard 1.30.0。
- `tools/Pack.ps1` / `PackUnityRelease.py`：四个功能包打包，基础 SDK 内置三个配套 tgz 和 Editor 安装器、版本依赖一致性、模型与 AAR/原生源码对应检查、确定性归档和校验清单。
- 发布包包含生成 AAR，源码 Git 继续忽略 AAR；本地修改以源码校验清单、Android diff、Cardboard patch/参考网格和 base revision 记录，不冒充干净提交构建。
- 正式 SDK 内的 Android XR 配置和 Gradle 回调，安装者不复制 Demo 脚本，不依赖开发仓库路径或原生编译目录。
- 可导入的纯显示/3DOF、模拟手、真实 UVC 和标准交互示例；交互样例交付官方手势所需的 prefab/形状引用，组件源码从 Unity 包原样导入。
- 新工程安装/升级、兼容矩阵、故障排查见 [安装文档](SDK_INSTALL.md)，同一说明随交付复制为 INSTALL.md。

## 使用与构建边界

当前基础 tgz 自动安装全部配套包；纯显示场景不创建相机或手势推理组件，标准 TrackedPoseDriver 读取 HMD 旋转。
手部及交互通过相应场景启用，应用接口保持 Unity XR / XR Hands / XRI。推理后端为 SDK 提供者配置，默认 CPU / VIDEO。
用户在自己的工程设置 Application Identifier 和 Build Settings；SDK 配置不替换它们。同名场景创建不覆盖已有场景。
Android SDK Platform 34、Build Tools 30.0.3、Unity Java/NDK 和首次 UPM/Maven 网络仍是消费工程的工具链要求，不随发布包提供整个离线工具链。

本交付是本地开发预览候选，没有推送制品仓库。Unity 6/团结/iOS/URP/其他硬件、精确标定和光学仍未认证；USB 授权确认继续保留 UVC-PERM-001。

## 验证方式

`tools/ValidateXRHandsStage5.ps1` 校验整个交付目录，复制 tarball 到隔离验收目录，Packages manifest 只引用相对路径的发布归档。
全新工程检查 SDK 设置幂等、应用设置保留、所有示例的脚本和 prefab 引用、官方手势形状、标准 XRHandSubsystem 实际运行及场景切换。
仅显示工程检查场景中没有手部相机或推理组件。升级验证从固定提交 `4cbde6bac79762a55522aafd702fa1977797bcaf` 的实际基础包 0.2.0-preview.9 开始，保留已序列化的 SpatialRuntimeHost、应用场景字节/GUID、Application Identifier 和 Build Settings。PrepareXRHandsStage5.py 支持通过 --baseline-revision 显式指定另一条升级基线；本次 SDK 提交不会改变默认旧版本。
升级基线测试使用现有 Android 桥接 AAR，但只验证 Core/Host 序列化兼容，不证明旧二进制行为或已公开发布版本的升级。
两个新工程用 SDK 菜单构建独立 Application Identifier 的 APK，后续记录实机显示、3DOF、CPU UVC 启动和无相机的纯显示模式。

## 2026-10-12 验证结果

发布归档安装、示例引用、官方手势、标准子系统模拟运行、仅显示工程和升级检查通过：交互检查 15 项、Play Mode 检查 10 项、仅显示检查 18 项、升级检查 6 项。升级时 Unity 首次刷新曾报告旧 PackageCache 源文件不存在，重新解析后编译和升级检查通过。

日志与 APK 根目录：`build/validation/xr-hands-stage5-cafcdfe925354853a25fe49733990bd2/`。两个 Android APK 均使用 SDK 的构建入口成功生成，没有复制 Demo 的原生准备或 Gradle 脚本。

| 工程 | APK | SHA-256 |
| --- | --- | --- |
| 完整交互 | `full/Builds/Android/spatialai.apk` | `a2c509e264d66371eff96205bce96c5475b9be68f1b0ac9a83db2762c8bb733e` |
| 仅显示 | `display/Builds/Android/spatialai.apk` | `b096f5ce6526444d9d3a225612c1d6ce22b3362737653df76a521acd1a3cb0e1` |

手机 ADB 可达，但安装均返回 `INSTALL_FAILED_USER_RESTRICTED: Install canceled by user`，因此这两个新 APK 的实机显示、3DOF、CPU UVC 和仅显示不启动相机尚未验收；不将之前 Demo 的实机结果代替本次消费工程验收。

交付目录：`dist/0.2.0-preview.10/unity/`；包含四个 UPM tgz、安装说明、第三方说明、源码快照与校验清单。长时间、空间精度与跨硬件验收保留独立待办。

用户工程 `D:/code/unity/spatialai-sdk-test-demo` 已保留原三个本地包并新增 XR Interaction；导入官方 Gestures，生成 UVC 与模拟交互场景，配置 Android XR 并将 UVC 场景加入 Build Settings。当前 Unity 编辑器无编译错误；六个官方检测组件引用检查通过。Play Mode 确认标准子系统运行、双手有效、食指关节有效和两个 UI 按钮存在；返回 UVC 场景。该检查使用模拟输入，用户工程自己的 Android 构建与真实 UVC 交互仍待运行。

后续用户确认该工程自行编译的 APK 手势识别场景可以正常使用。这是本地源码包接入工程的人工确认，发布 tgz 包的干净工程导入验证继续单独执行。已按用户要求清理该工程的 SDK/Cardboard 引用及生成场景，旧示例/资产移至工程内 Assets 外的保留目录，由用户自行导入 dist 发布包并按文档创建场景。

## 基础 SDK 作为单 tarball 安装入口

当前通过 Package Manager 的 Add package from tarball 导入 `com.spatialai.sdk-0.2.0-preview.10.tgz` 一次，基础包的独立 Editor 程序集自动安装 Payload~ 中的 Cardboard、XR Hands、XR Interaction 三个配套包。基础包不包含自身副本，不声明未注册的本地依赖。安装后工程只有四个功能包，没有独立 installer 包。
基础 SDK 和三个配套归档校验后保留在用户工程 SDKPackages 下，引用使用相对路径；其他依赖、应用场景/设置继续保留。ProjectSettings/SpatialAISdkInstallationState.json 支持重载、重启、完成后幂等和失败后手动 Retry Installation。旧独立 installer 的程序集/状态与新版隔离，升级时移除旧包引用，保留历史归档和备份。
旧的 PowerShell 安装脚本、Embedded ZIP 和独立 installer 源包/发行制品已移除。源码 checkout 没有 Payload~，不会自动安装。安装后仍需执行 Configure Android XR，再创建场景。

此前独立 installer 已通过自动安装四包、重载、校验失败/重试及工程搬迁验证，日志保留在 build/validation/upm-installer-63e886fee49d4df5b3882a5daec4f88e/，供历史参考；不作为新版基础包合并后的验收结果。
新版验证使用 tools/ValidateUnityInstaller.ps1；加上 -LegacyInstaller '<旧 installer.tgz>' 可以额外验证旧独立入口升级。

### 基础包合并后的验收结果（2026-10-12）

Unity 2022.3.14f1 全新工程仅导入基础 SDK tgz，三个内置配套包自动安装，四包及官方依赖解析、编译通过。真实程序集重载后保持幂等；移走原下载文件后仍正常工作。破坏配套归档时校验失败且 manifest 保持不变，恢复后手动 Retry 成功。只复制 Assets、Packages、ProjectSettings、SDKPackages 到包含空格的新路径后，重新打开、解析和编译通过。

- 全新工程日志：`build/validation/upm-installer-0ce54dbb70164b8facc013beda6d3643/consumer/installer-validation.log`；搬迁日志位于同根目录 `relocated project/installer-validation.log`。
- 旧入口升级日志：`build/validation/upm-installer-655dc6fabddf4339b5591dd29a61f521/consumer/installer-validation.log`；搬迁日志位于同根目录 `relocated project/installer-validation.log`。
- 两组结果均为 `SPATIALAI_INSTALLER_PASS automatic=3` 和 `SPATIALAI_INSTALLER_RELOCATION_PASS`，Unity 退出码为 0。升级确认旧 installer 引用已移除，历史归档/备份和应用资产保留。

旧入口升级过程中，Unity 在切换/删除包时短暂报告旧 PackageCache 源文件不存在（CS2001），后续自动刷新并完成编译；搬迁后的全新 Library 编译无该错误。归档校验失败日志为测试预期负例，测试结束已恢复归档。
本次只修改 Editor 安装与打包流程，运行时代码、AAR、模型及原生插件未变；未重新构建或安装手机 APK。用户 Demo 工程未代为升级。

### 用户最终确认（2026-10-12）

用户在导入基础 SDK tgz 并自动安装三个配套包后明确反馈“验证没有问题了”。结合此前消费工程 APK 手势场景正常、Android XR 配置后显示正常的反馈，本轮 SDK 安装接入与基础功能人工验收已确认。用户未提供新 APK 哈希和逐项量化数据，不将该反馈扩展为所有历史测试 APK、性能或空间精度验收。
阶段 0～5 的本轮软件接入与开发预览交付已完成。保留 USB 授权、实测标定/光学尺度、长时间稳定性/温升和跨机型验证待办。修改文件与验证工具去留见 [改动与暂存清单](UNITY_XR_HANDS_CHANGE_REVIEW.md)。

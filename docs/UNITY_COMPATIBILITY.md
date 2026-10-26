# Unity 2022.3 / Unity 6 兼容性

2026-10-26，源码版本：基础 SDK `0.2.0-preview.11`，XR Hands / XR Interaction `0.1.0-preview.3`。
UPM 的 `unity: 2022.3` 是最低版本，保留它才能让同一份包同时用于 2022.3 和 Unity 6。

## 本次修改

- XR Interaction 通过 asmdef 的包版本定义区分 XRI 2/3；XRI 3 使用新的 Interactors、Interactables、Visuals 命名空间，并将选择和 UI 按压绑定到 Input Readers。保留 2022 的控制器输入路径。
- Unity 6 Android 保留编辑器生成的 compile/target SDK、Build Tools、Gradle/AGP/R8；Unity 2022 继续使用既有 API 34、Build Tools 30.0.3 与 R8 8.3.37 修复。最低 Android API 仍为 28。
- Configure Android XR 在 Unity 6 使用自动目标 SDK、单一 Input System 后端；2022 保留 Both。旧 HelloSpatialXR 示例也改为支持 Input System 指针。
- 官方手势导入同时补齐 HandVisualizer 与 TMP Essentials，兼容新版 XR Hands 的项目校验要求；异步资源导入完成后才结束 CLI 验证。
- 显示物体、骨架、射线、圆圈和交互方块使用内置于 Resources 的 unlit shader，兼容 Built-in/URP 并保留在 Player 中。没有强制更改用户工程的渲染管线。
- 保留工作区原有的 12 mm 圆圈修改；将过时的圆圈尺寸和 HUD `Capture` 文案断言同步到现有行为。

## 编辑器实测

| 编辑器 | XR Hands / XRI | 渲染管线 | 契约与交互回归 | 渲染像素 | Android APK |
| --- | --- | --- | --- | --- | --- |
| 2022.3.14f1 | 1.4.3 / 2.5.4 | Built-in | 47 项契约 + 172 项交互通过 | 通过 | 通过 |
| 6000.0.84f1 | 1.7.3 / 3.0.11 | Built-in | 47 项契约 + 172 项交互通过 | 通过 | 通过 |
| 6000.6.2f1 | 1.9.0 / 3.6.0 | URP 17.6.0 | 47 项契约 + 172 项交互通过 | 通过 | 通过 |

各版本另通过 15 项配置检查，包括 Gradle 转换幂等、手部依赖、2022 工具链修复与 Unity 6 工具链保留，以及 UnityPlayerActivity / UnityPlayerGameActivity 的 USB 关联声明。
交互测试包含标准 XRI 方块选择、跟踪丢失取消、重新武装、UI 点击、独立双手指针和 20 次样例启停/销毁循环；每个编辑器得到 7 次预期 UI 点击。
渲染测试使用实际 GPU、相机与 RenderTexture 读取中心像素，三个版本均为青色 `RGBA(0,1,1,1)`。
三个版本另验证全部示例代码导入、四个随包场景无丢失脚本，官方手势与 TMP Essentials 资源导入完成。
三个 APK 均由 Unity 的完整 `BuildPlayer` 流程生成，路径为各验证工程的 `Builds/Android/spatialai.apk`（约 59 / 62 / 69 MB）。未安装到真机。

日志和渲染图位于 `build/validation/unity6-compat/{2022.3,6000.0,6000.6-urp}/`，不会进入源码 Git。
这些编辑器测试不等于真机 Cardboard 双目、UVC 权限/取流、IMU 跟随或光学标定验收。HDRP、团结、iOS 未纳入本次验证。

## 复验

```powershell
.\tools\ValidateUnityCompatibility.ps1 `
  -UnityPath 'D:\unity\hub\editor\6000.6.2f1\Editor\Unity.exe' `
  -UrpVersion '17.6.0' -BuildAndroid
```

更换 UnityPath 可验证 2022.3 或 6000.0；不指定 UrpVersion 时创建 Built-in 隔离工程。不传 BuildAndroid 时只验证导入、配置、模拟交互和渲染。
脚本不会打开或升级 `samples/unity/SpatialXR`。Unity 6 构建验证临时使用相应编辑器内置 SDK，并在结束时恢复此前 SDK 路径；正常消费者仍应在 External Tools 配置正确工具链。

本机 Windows 的 Temurin 17.0.18 在系统临时目录使用 AF_UNIX 时出现 `Unable to establish loopback connection`。直接调用 Gradle 时可对 Java 设置 `JAVA_TOOL_OPTIONS=-Djdk.net.unixdomain.tmpdir=D:\visor-x\java-tmp`。Unity 调用 Gradle 未传递此选项，因此本轮完整 Unity 构建对启动进程设置 `TEMP`、`TMP` 为同一工作区临时目录；三个 APK 均已构建成功。没有改变系统 Java/网络/安全设置。重复构建可复用 `GRADLE_USER_HOME=D:\visor-x\spatial-ai-sdk\build\gradle-cache`。2022 还可通过既有 `SPATIALAI_R8_JAR` 指向本地 8.3.37 JAR。

## 用户 Demo 与发布包

`D:\code\unity\VisorX-Demo-unity6\Packages\manifest.json` 已添加四个本地源码包引用，保留了其原有 Unity 6.6 / URP 依赖。原 manifest 备份位于 `build/validation/unity6-compat/demo-manifest-before.json`。
本地源码接入需要保留 SDK 与 Cardboard checkout 的当前路径。需要可搬迁交付时应使用重新校验、打包后的 tgz。
Unity 6 首次打开共用源码时若出现 Script Updating Consent，选择 No，保留包内按版本编译的 XRI 2/3 分支；本次 Demo 已按此方式完成编译。
已加入 `Assets/Scenes/SpatialHandInteractionSimulation.unity` 及其 SDK 动作/prefab、官方手势依赖资源。打开该场景并 Play 可运行模拟手交互；1/2/3 切换手势，L/R 切换左右手跟踪，空格切换运动。原 SampleScene 保留。
用户工程当前默认构建/Play 场景为 `Assets/Scenes/ImuDiagnostics.unity`，应用名称为 `Spatial-XR-Unity6`；生成入口、验证结果和构建方式见 [Unity 6 IMU Demo](../samples/unity/Unity6/README.md)。

本轮未生成或替换 `dist` 发布包：现有 Android 仓库 HEAD 与锁文件 revision 不一致，且 Cardboard checkout 内 AAR 的原生库与 `build/native/cardboard-rhinox-v1301` 产物不同。发布前需单独整理原生制品的来源和校验关系。本次没有更改 Android 锁文件、Cardboard 补丁或这些二进制。

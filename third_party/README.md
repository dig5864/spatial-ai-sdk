# Cardboard 上游基线

当前 RhinoX Unity 2022 诊断工程使用 Cardboard v1.30.1；以下 v1.35.0 记录是更早的通用原生基线，不是该示例的构建版本。最新验证见 `docs/VALIDATION.md`。

已固定两个仓库的版本，并完成原生 SDK 的 Android arm64 编译。通用候选版本记录在 `cardboard.lock.json`；源码使用稀疏检出，保持上游文件不修改。Unity 2022 RhinoX 诊断版另行固定 Cardboard v1.30.1，在 `third_party/rhinox/` 保存原生补丁和网格数据，并通过 `tools/BuildRhinoXCardboard.ps1` 生成对应的 Android AAR。

| 项目 | 地址 | 候选版本 | 状态 |
| --- | --- | --- | --- |
| 原生 SDK | https://github.com/googlevr/cardboard | v1.35.0 / 5969239e7c87f4cd64c8ec170ce1e7f4eb559e37 | 原生库编译通过，包含 Unity provider；未运行实机 |
| Unity XR Plugin | https://github.com/googlevr/cardboard-xr-plugin | v1.35.0 / 36ac9815b8f191fe11e149b7f323368fa86655a6 | 已检出 Runtime、Editor、Resources、Sample 与 Android AAR；Unity 2022.3 本地导入和脚本编译通过 |

候选版本不代表已通过团结引擎或眼镜适配。上游代码放入 `third_party/checkouts/`，默认不随 Spatial AI SDK 包发布。公共接口使用自有类型，修改集中在 Adapter 与构建补丁中。

下一步：先运行官方 HelloCardboard，再实现 `IHeadPoseSource` / `IOpticsProvider` / 渲染线程 provider；外接显示单独验证。Cardboard 的手机头姿不能直接用于未与头部固定的手机。

## 获取与构建

在 SpatialAISDK 根目录执行：

```powershell
powershell -File tools/FetchCardboard.ps1
powershell -File tools/FetchCardboard.ps1 -VerifyOnly
powershell -File tools/BuildCardboard.ps1 -NdkPath '<Android NDK 路径>'
```

需要 Git、CMake >= 3.22.1、Ninja 和 Windows x64 Android NDK。脚本验证 origin、完整提交号和工作区状态；发现本地改动直接停止，不覆盖已有修改。检出路径受 lock 文件管理。

默认构建输出在 `build/native/android/arm64-v8a/cardboard/`：原生库、SHA-256/导出符号/依赖库报告，以及上游 Apache 2.0 许可证。构建不依赖 Gradle，也不产出 AAR/APK。原生 SDK 的 Java/protobuf/AndroidX 依赖仍需在完整 Android 构建中处理。

本机 NDK 23.1 编译通过只说明该原生配置可构建；上游 sdk/build.gradle 指定的 NDK 为 29.0.14206865。发布工具链、Android 16 KB page size、Unity 6 与团结适配仍需独立验证，不能用当前结果代替。

原生 SDK 仍为稀疏检出；Unity XR Plugin 已补齐可导入内容，保持固定提交和干净工作区。官方 HelloCardboard 样例尚未真机运行。Cardboard AAR 要求 Android 编译 SDK 35，本机 Unity 2022.3 所带 SDK 32 导致 XR 示例 APK 的 Gradle 打包失败；需在具备 SDK 35 的工具链继续验证。

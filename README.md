# Spatial AI SDK

**本目录是当前 Spatial AI SDK 的主工程。** `E:\wuyc\visor-x\SDK\xms-xr-sdk` 是历史参考工程；两者的包名、API 和完成范围不同。当前产品的新增功能与验收以本目录为准，历史代码需要核对后再迁入。

Unity、WebXR、OpenXR 和 Native 的目录归属见 [多平台目录设计](docs/REPOSITORY_LAYOUT.md)。本次整理没有改变当前预览包的导入路径。

预览框架 `0.2.0-preview.4`。统一 Runtime 与应用接口，同时面向 Unity 6 和团结引擎。Android 与 iOS 共用 Unity 包；先使用 RhinoX Pro 调试，手机兼容列表待定。

## 当前可以做什么

2026-09-28 更新：已在 RhinoX 真机安装并运行独立 IMU 诊断 APK，真实传感器进入 Cardboard EKF 并输出四元数。构建及验证边界见 [真机诊断说明](samples/android/imu-diagnostic/README.md)。它尚未接入 UPM 包的 Spatial Runtime。

- 导入本地 UPM 包，创建模拟场景，调节头姿和手柄方向，观察射线、悬停高亮和扳机反馈。
- 打开 [SpatialXR Unity 消费工程](samples/unity/SpatialXR/README.md)，检查 Cardboard XR 基线 Sample、设备 ID 匹配及 Android/iOS XR Loader 配置。
- 运行独立核心测试，检查生命周期、暂停恢复、输入边沿、故障时清除旧姿态和未标定光学配置。
- 编译原生基础库，验证 C ABI 及 C# 调用边界。

已额外完成固定版本 Cardboard 源码获取与 Android 原生库独立构建。现新增可选 Cardboard XR 基线 Sample 与按设备 ID 查找光学 Profile 的逻辑；Cardboard 插件负责其自身的 XR Display/Input，尚未连接 SpatialSession 或 RhinoX OST 参数。设备通信、独立手柄和 AI 调用仍未实现。旧 `SpatialRuntimeHost` 选择硬件 Backend 时明确返回 Unsupported。

核心/原生互调、C ABI、Unity 2022.3 导入与 Android arm64 基础库的验证记录见 [验证记录](docs/VALIDATION.md)。`tools/Pack.ps1` 将本地预览候选包输出到 `dist/<版本>/unity/`；该产物尚未正式发布。

## 目录

```text
runtime/                    C ABI、Core、设备适配与光学参考元数据
packages/com.spatialai.sdk/  本地 UPM 包、Unity Host、Editor、Sample
samples/android/             独立 IMU 诊断应用
samples/unity/               Cardboard XR 消费工程
tests/                       Core、原生互调和 Unity API 编译检查
tools/                       内部构建/验证脚本（不属于产品 CLI）
third_party/                 Cardboard 版本锁与上游源码
docs/                        架构、兼容矩阵与验证记录
build/                       编译中间文件与历史构建记录（不入 Git）
dist/                        本地发布候选包（不入 Git）
```

## 在编辑器中开始

1. 新建空白 Built-in 3D 项目。
2. Package Manager → Add package from disk，选择 `packages/com.spatialai.sdk/package.json`。
3. 导入包中的 Hello Spatial (Simulator) Sample。
4. 菜单 Spatial AI → Create Simulator Scene，保存场景并进入 Play。
5. 用滑条控制头部、手柄方向，按住 Trigger 触发命中；菜单 Check Setup 可输出版本和状态。

Unity 6 与团结引擎仍需分别验证，本预览不宣称任何硬件组合已认证。Unity 2022 示例已切换到 Cardboard XR Plugin v1.30.1，并以 Android SDK 34 完成 Gradle APK 打包；尚未在当前轮次完成 Unity 菜单重新构建和眼镜画面验收。详情见 [兼容性说明](docs/COMPATIBILITY.md)与[验证记录](docs/VALIDATION.md)。

## 开发者验证

纯核心：`dotnet run --project tests/CoreChecks/CoreChecks.csproj`。

Windows 完整验证：`powershell -File tools/Validate.ps1`，需要 .NET 8、CMake 和 C/C++ 编译器。首次配置 MinGW 时可显式使用 Ninja，并设置 CMAKE_C_COMPILER 与 CMAKE_CXX_COMPILER。可选 UnityManagedDir 指向编辑器的 Data/Managed 目录以进行引用编译；它不等于编辑器运行测试。

Android 原生基础库：`powershell -File tools/BuildAndroid.ps1 -NdkPath <NDK目录>`。默认 API 26/arm64 只是初始构建目标，未构成设备支持承诺。

## 下一步

Cardboard 固定版本与原生构建已完成；下一阶段是完整官方 APK 基线 → Spatial 头姿诊断 → RhinoX Pro 接入 → XR Display/Input provider → 左右眼光学与 OST 网格 → 两编辑器实机闭环。接入边界与验收见 [Cardboard 集成说明](docs/CARDBOARD_INTEGRATION.md)，构建方法见 [上游说明](third_party/README.md)。Spatial AI 模型、Spatial Tools、企业授权与计费按设计需求后续推进。

总体设计见同级目录之外的 `SDK/Spatial_AI_SDK_设计需求.md`。

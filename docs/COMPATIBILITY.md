# 兼容与验证状态

| 项目 | 状态 |
| --- | --- |
| 纯 C# Core | .NET 8 核心及原生互调共 26 项检查通过 |
| Windows 原生 C ABI | CMake + MinGW 编译及 C ABI 测试通过 |
| Unity 6 | V1 必须支持；本机未发现已安装版本，尚未运行 |
| 团结引擎 | V1 必须支持；具体版本待定，尚未运行 |
| Unity 2022.3.14f1c1 | Spatial AI 与 Cardboard 本地包导入、XR Sample 编译/场景生成、Android XR Loader 配置通过；Cardboard v1.30.1 与 Android SDK 34 的已生成 Gradle 工程成功打包 APK，Unity 菜单构建回归待验证 |
| iOS / iPhone Cardboard 基线 | 固定版本插件内含 iOS 原生库；消费工程已加入 iOS XR Loader 配置及 Xcode 工程导出入口。当前 Unity 2022 的 Package Manager 在重新解析本地包时出错，因此新增 Editor 脚本尚未完成编辑器编译；尚未在 macOS 导出、签名或 iPhone 运行 |
| iOS / RhinoX 外接眼镜 | 待验证 iPhone 到眼镜的连接、独立头姿、显示路由和 OST 标定；当前不宣称支持 |
| Android arm64 | NDK 23.1 / API 26 原生基础库编译通过；XR Sample 的托管与 IL2CPP 编译通过；Android APK 已生成并通过签名校验，真机显示未验收 |
| RhinoX Pro | 首个调试目标；独立 IMU 诊断 APK 曾在真机运行；Unity XR Sample 的光学/显示未验收 |
| 兼容手机 | 待定 |
| 渲染管线 | 模拟示例以 Built-in 为首轮验证目标；URP 待验 |
| 真实双目显示、跟踪、手柄 | 可选 Cardboard XR Plugin 提供基线显示和手机头姿，尚未真机验收；SpatialSession 硬件 Backend 仍返回 Unsupported；RhinoX 光学与手柄未实现 |

发布 V1 前，各编辑器各选一个准确版本，在 Android 手机上完成文档中的完整链路；记录手机、Android、固件、图形 API、profile、帧率、时延与插拔结果。

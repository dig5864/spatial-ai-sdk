# Cardboard Adapter 接入清单

此目录预留原生适配实现；目前硬件选择返回 Unsupported。

已完成：固定 v1.35.0 上游源码、独立编译 Android arm64 原生库（含上游 Unity provider），并校验关键导出。尚未实现这里的 Spatial Runtime 适配器。详见 `docs/CARDBOARD_INTEGRATION.md`。

1. 锁定原生 SDK 与 XR 插件的完整提交号，验证原版 Android Sample。
2. 将上游 pose 转换为统一左手坐标、xyzw 四元数和单调时钟 ns。若使用眼镜 IMU，独立验证融合入口；不默认复用手机传感器。
3. 每眼投影、眼位和 OST 网格由 OpticsProvider 决定，仅执行一次最终畸变。
4. 在 Unity XR provider 图形线程实现渲染目标、纹理所有权、同步与提交；严禁从 MonoBehaviour.Update 直接调用渲染线程接口。
5. 原生 provider 的头文件/接口版本由 Unity 6、团结引擎分别验证。记录 OpenGLES/Vulkan 支持，不伪造 Subsystem Manifest 或成功的 XRLoader。

当前包没有 XRLoader，也不会在 XR Plug-in Management 中宣称可用。真正 provider 完成后再注册 Display/Input 子系统和对应 manifest。

# Cardboard 版本与后续升级

当前继续使用 Spatial 定制的 Cardboard SDK 1.30.1 和 Unity 插件 1.30.0，不因升级而改变当前交付。

官方 1.36.0 要求 Unity 6000.0.58f2 或更新，不能直接替代仍支持 Unity 2022.3 的交付。当前光学、手机屏幕和追踪等原生补丁在 1.36.0 源码上通过了 `git apply --check`，这不代表已经完成构建或设备验证。1.36.0 的 GLES 眼睛纹理和 XR 描述符仍采用与旧版相同的关键实现，升级不能视为 Linear 色彩问题或卡顿的修复。

如未来决定升级，最终分别交付两个 SDK `.tar.gz`：Unity 2022 对应现有兼容版本；Unity 6 对应经过迁移的 1.36.0。每个包只安装一个 Cardboard 版本，包含配套的 Spatial 修改和依赖；需要分别验证光学与追踪、手机与 RhinoX、Built-in 与 URP、Gamma 与 Linear，以及受支持的图形 API。不得把实验性的帧时序追踪或未验证的配置默认作为正式优化。

当前 1.30.1 的 GLES3 路径按 Unity XR 的 sRGB 请求选择眼睛纹理格式，并使用一致的 XR 纹理描述符；其他图形后端保持原有行为。2026-11-03 在 RhinoX、Unity 6000.3.27f1、Android GLES3 的静态棋盘截图中，URP + Linear、URP + Gamma、Built-in + Linear 和 Built-in + Gamma 的暗格分别为 RGB (30,41,58)、(30,40,58)、(31,41,59)、(31,41,59)，对应场景设定约 (31,41,59)。Demo 为兼容 Built-in 移除显式 SRPDefaultUnlit Pass 后，URP + Linear 的最终构建复测为 (30,41,58)。该结果不代表 Unity 2022、普通手机、Vulkan、iOS、XRI 或不同 HDR 和后处理配置均已验证。

# Spatial AI SDK 0.1 架构

应用 → 共用 C# API → SpatialSession → IRuntimeBackend → Simulator / 后续设备适配。

原生方向：引擎 XR Provider → 稳定 C ABI → Spatial Runtime → Cardboard、设备与光学 Adapter。`runtime/` 当前只实现 ABI 与资源所有权基础；未接硬件时 Start 返回 Unsupported、能力为 0、姿态有效位为 false。

## 生命周期与数据契约

- Start：Stopped/Suspended/Error → Starting → Running 或 Error；失败释放后端并清空快照。
- Tick：仅 Running 可读；后端断连/异常导致 Error，旧姿态和按键立即清空。应用可以显式 Start 重试。
- Suspend/Stop：释放会话并清空快照；Resume 通过 Start，不能沿用暂停前扳机按下状态。
- Dispose：幂等，之后禁止重启。一个 Session 拥有一个 Backend；API 从主线程串行调用。
- 姿态：Unity 左手坐标（右/上/前），xyzw 单位四元数，ns 单调时钟，显式 IsTracked。只有旋转有效，3DoF 虚拟手柄起点不代表实际手柄位置。
- 模拟与硬件：IsSimulated 和 Capabilities 是显式标记；模拟器不声明 StereoDisplay。Running 只代表对应后端的会话运行，不代表 P0 硬件功能全部可用。
- `OpticsProfile.Validate` 只验证元数据和标定状态，不加载或验证网格；后续必须增加 frustum、左右眼外参、网格完整性和设备匹配检查。

## 引擎兼容

Unity 6 与团结引擎均是 V1 支持目标，具体版本待双端实测。当前 UPM manifest 的 2022.3 是最低 API 目标，不能据此宣称团结或 Unity 6 已认证。共用源码对引擎差异使用薄适配层；真正 XR 原生 provider 可以分别编译。

## 后续能力边界

Spatial AI 能力接口 → 模型适配层 + Spatial Tools → Runtime。上层编排不引用模型供应商类型。相机、语音、世界定位、手势、模型调用、企业租户、Device License、AI Usage、内容服务在 0.1 中均未实现。

新能力应先定义可用性、权限、时间戳、来源、坐标和结果有效性，再增加模型/工具实现。仅 3DoF 设备的输出不得声称世界锚定。开发 SDK 免费的产品定位不等同于已决定源码开源许可证；授权发布方式待项目方确定。

# Unity XR Hands 阶段 0：依赖与空间数据契约

更新日期：2026-10-09。本阶段交付代码契约、可选包边界、依赖基线及验证工具。
此处记录阶段 0 当时的基线；后续已实现 [阶段 1 模拟 Provider](UNITY_XR_HANDS_STAGE1.md)。
阶段 0 未注册 `XRHandSubsystem`，未把现有 UVC 关键点转换为真实三维关节。
后续执行顺序见 [分阶段计划](UNITY_XR_HANDS_IMPLEMENTATION_PLAN.md)。

## 1. 固定依赖与安装边界

| 组件 | 本轮固定版本 | 用途/验证范围 |
| --- | --- | --- |
| Unity Editor | 2022.3.14f1（本机实际版本） | Windows 编辑器最小工程；不是 Unity 6/团结认证 |
| 基础包 `com.spatialai.sdk` | 0.2.0-preview.9（本地源码） | 原有 API，新增无 Unity 依赖的空间契约 |
| 配套包 `com.spatialai.xr-hands` | 0.1.0-preview.1（本地源码） | 独立 `SpatialAI.XRHands` 程序集，依赖 XR Hands |
| XR Hands | 1.4.3 | 标准关节 ID 和手别；包声明最低 Unity 2021.3 |
| Input System | 1.7.0 | 与现有 Cardboard 输入基线一致 |
| XR Plug-in Management | 4.4.0 | 满足 XR Hands 的 4.0.1 最低依赖 |
| Cardboard 本地插件 | 本地 1.30.0 包清单，源码基于 1.30.1 补丁 | 可选共存编译；不提供手部子系统 |
| Unity UI (UGUI) | 1.0.0 | Cardboard Reticle 的 EventSystems 依赖；共存验证时显式引入 |
| XR Interaction Toolkit | 未引入、未固定 | 阶段 4 再验收；当前不需要 OpenXR/Meta Aim |

后续补充（2026-10-10）：[阶段 4](UNITY_XR_HANDS_STAGE4.md) 的独立可选包固定 XRI 2.5.4；
上表保留阶段 0 当时的基线，基础 SDK 与 XR Hands 包仍不强制引入 XRI。

Unity 官方元数据：[XR Hands](https://packages.unity.com/com.unity.xr.hands) 和
[XR Management](https://packages.unity.com/com.unity.xr.management)。XR Hands 1.4.3 另依赖
Mathematics 1.2.6、XR Core Utils 2.2.0、XR 模块 1.0.0；实际传递依赖以每个验证工程
`Packages/packages-lock.json` 为准。项目显式固定三个顶层 Unity 包，防止其他包升级后静默改变基线。

基础包的 `package.json` 不增加 XR Hands 依赖，其空间契约在 `SpatialAI.Core`。
缺少 XR Hands 时根本不安装配套包，无须用条件编译隐藏残缺程序集。
安装配套包时由 UPM 解析其依赖；本地 SDK 尚未在公共 registry 发布，因此必须显式指定两个本地包路径。
示例 manifest 见 [配套包 README](../packages/com.spatialai.xr-hands/README.md)。
现有 `samples/unity/SpatialXR` 工程尚未添加配套包，原有自定义手势场景继续运行；
其旧的 XR Management 解析结果不能视为本轮 XR Hands 基线。
仓库 `Pack.ps1` 目前只打包基础包，配套包的制品发布与新工程 Android 验收留到阶段 5。

## 2. 已实现的数据契约

代码：[`HandContracts.cs`](../packages/com.spatialai.sdk/Runtime/Core/HandContracts.cs)。
`ISpatialHandsFrameSource.TryGetLatestFrame` 约定主线程无阻塞读取最新的不可变快照。
本阶段只定义接口，不实现模拟源、UVC 空间转换或源生命周期。

| 类型/字段 | 约定 |
| --- | --- |
| `SpatialHandsFrame` | 统一空间帧，固定左右手槽；缺失手用 `Untracked`，不以数组顺序猜手别 |
| `SourceId` / `Source` | 来源会话标识；Simulation / MonocularEstimate / HardwareTracking |
| `Sequence` | 来源内单调递增；重开源时更换 SourceId；同一快照多次读取不增加序号 |
| `OriginRevision` | 归零或标定变更时递增；消费者不能跨版本插值，更新时须清除旧姿态 |
| `SourceTimestampNs` / `SourceClock` | 来源时间；未知必须用 -1/Unknown；UVC 回调时间不等同于传感器曝光时间 |
| `ReceivedTimestampNs` / `ReceivedClock` | 桥接接收时间；HostStopwatch 或 AndroidElapsedRealtime，来源须记录实际时钟 |
| `TryGetSourceToReceiveNs` | 仅同一已知时钟域且接收晚于来源时返回值；跨时钟域/未知返回 false 和 -1 |
| `SpatialHandSample.Root` | tracking origin 空间内腕部根姿态；只支持位置/旋转，各自标记有效性 |
| `GetJoint` | 26 个固定槽位；数组复制后不可外部修改，`Count` 不是有效关节 |
| `HandJointSample.Valid` | Position / Rotation / Radius / LinearVelocity / AngularVelocity 按本帧实际数据逐项声明 |
| `HandJointSample.Estimated` | Valid 的子集；标记估算属性；不能将插补关节或单目深度伪装成测量值 |
| `SpatialHandCapabilities` | 来源支持的根/关节属性及必须标记为估算的属性；Validate 检查能力与帧的一致性 |

位置单位 m、半径 m、线速度 m/s、角速度 rad/s，旋转为单位四元数 xyzw。
有效标记中的非有限位置/速度、非单位旋转、非正半径会被拒绝。
无效分量默认零仅用于存储，不代表有效零姿态；失去跟踪必须同时清空根与所有关节的有效标记。
部分关节丢失时手可保持跟踪，但对应关节无效；阶段 1 Provider 要按 XR Hands 的状态规则发布。
序号、帧年龄和 OriginRevision 的跨帧检查属于后续输入源/Provider，当前构造器只验证单帧。

旧 `SpatialHandTracking` / `SpatialHandFrame` / `SpatialHand` 和自有 `SpatialGesture` 保持兼容。
它们仍服务于图像预览和已有交互。新接口不自动转换旧数据，不以同名字段混用坐标。
原始 UVC 图像关键点对应的空间能力是 `SpatialHandCapabilities.None`；
等阶段 2 提供标定与深度方案后才允许填入新空间帧。

## 3. 坐标与光学变换

统一手部姿态使用 **Unity tracking origin**：+X 右、+Y 上、+Z 前，米制。
`HandSide` 表示人的真实左/右手，不由屏幕左右位置或相机镜像推断。
MediaPipe 的手别需结合输入图像是否镜像、相机朝向实测转换；当前映射助手不自动翻转手别。

约定 `T_A_B` 将 B 空间的点转换到 A 空间，`R` 是旋转矩阵、`t` 是平移。
完成畸变校正、图像旋转/镜像还原及单目深度估算后，采用：

```text
p_origin_joint = R_origin_head * (R_head_camera * p_camera_joint + t_head_camera) + t_origin_head
R_origin_joint = R_origin_head * R_head_camera * R_camera_joint
```

`head` 是物理头部安装参考；`camera` 的轴向先转换到兼容 Unity 的定义。
光学镜像或手性转换用矩阵表达：旋转基变换为 `R_new = C * R_old * inverse(C)`，
不能把一个反射矩阵直接存成普通旋转四元数。
相机内参必须对应实际 640×480 图像及其裁剪/旋转；相机到头部的外参必须由安装标定取得。

当前手机横放、屏幕向外、向下倾约 45°、一次镜片反射。
`HostImuProfile.Map` 的安装修正与 `ReflectedViewRotation.Apply` 的显示补偿用途不同。
不能直接使用补偿后的 `qView` 当作物理头姿，也不能直接把预览四边形的坐标当作手部世界坐标。
阶段 2 须明确 Cardboard 头部参考点到物理安装参考点的变换，再结合显示补偿做镜片内对位验证；
已有画面轴向反馈不等于这些外参已标定。

3DoF 没有真实头部平移：`t_origin_head` 仅是固定的模型偏移/配置值，能力不能声明为跟踪平移。
禁止由此承诺房间尺度固定手位置。归零后手和头共享新的 OriginRevision，避免坐标断层。
MediaPipe world landmarks 的局部米制骨架也不提供手在相机前的绝对根位置，需单独求解。

## 4. 21 → 26 关节对应与能力边界

对应助手：[`SpatialXRHandsContract.cs`](../packages/com.spatialai.xr-hands/Runtime/SpatialXRHandsContract.cs)。
SDK 索引从 0 开始，Unity 的 `XRHandJointID` 数值不直接作为数组下标；按名称映射。
下表只是骨架对应，**没有执行深度、尺度或朝向估算**。

| SDK / Unity 同名关节（SDK 槽位） | MediaPipe 索引 | 阶段 2 方案要求 |
| --- | --- | --- |
| Wrist (0) | 0 | 需绝对根位置；Root 与 Wrist 使用同一姿态来源 |
| Palm (1) | 缺失 | 由掌部结构估算或暂不支持 |
| ThumbMetacarpal / Proximal / Distal / Tip (2–5) | 1 / 2 / 3 / 4 | 位置经空间求解；朝向另行推导 |
| IndexMetacarpal (6) | 缺失 | 估算或暂不支持 |
| IndexProximal / Intermediate / Distal / Tip (7–10) | 5 / 6 / 7 / 8 | 同上 |
| MiddleMetacarpal (11) | 缺失 | 估算或暂不支持 |
| MiddleProximal / Intermediate / Distal / Tip (12–15) | 9 / 10 / 11 / 12 | 同上 |
| RingMetacarpal (16) | 缺失 | 估算或暂不支持 |
| RingProximal / Intermediate / Distal / Tip (17–20) | 13 / 14 / 15 / 16 | 同上 |
| LittleMetacarpal (21) | 缺失 | 估算或暂不支持 |
| LittleProximal / Intermediate / Distal / Tip (22–25) | 17 / 18 / 19 / 20 | 同上 |

| 属性 | 当前原始 UVC 数据的空间能力 | 后续声明规则 |
| --- | --- | --- |
| 根/关节位置 | 不支持发布米制空间姿态 | 有标定/深度后，单目来源标记 Estimated |
| 旋转 | 不支持 | 推导时标记 Estimated；退化/遮挡则无效 |
| 半径 | 不支持 | 无实际测量时无效；模型半径需标为 Estimated |
| 线/角速度 | 不支持 | 有连续同坐标、同时间基准数据后才可估算，归零时重置 |
| 手势类别/捏合 | 原有 SDK API 支持，质量尚未量化 | 与标准关节分开；不自动伪造 MetaAimHand 输入设备 |

阶段 1 模拟源可以发布确定的模拟姿态，但 `Source=Simulation` 必须保留，不能用于硬件验收。
必须明确每个关节支持哪些属性；`GetHandLayout` 的关节布局与本帧有效性分开处理。

## 5. 分阶段验收目标与测量

以下是开发验收初始目标，尚无真实三维手测量数据，**不是已达标结论或精度承诺**。
阶段 2 首轮测量后按实际可支持的能力调整，并在文档记录调整原因。
距离为相机前 0.3～0.7 m、静态光照、手动曝光 16.6 ms；超出此范围单列测试。

| 阶段/项目 | 初始目标 | 测量方法 |
| --- | --- | --- |
| 0 依赖/契约 | 两个全新最小工程编译成功；26 点/21 点对应唯一；没有额外手部子系统 | 本文脚本、CoreChecks、版本锁检查 |
| 1 模拟姿态 | 刚体变换位置误差 ≤ 0.01 mm、角误差 ≤ 0.01°；左右手独立 | 确定性输入对照标准 API；至少覆盖转头/抬头/侧倾组合 |
| 2 根/指尖位置 | 静态 P95 绝对位置误差 ≤ 30 mm | 固定距离与已知三维位置标记，多视野区域、双手各至少 100 样本 |
| 2 尺度 | 骨长/已知尺寸相对误差 P95 ≤ 10% | 已知尺寸与距离，测试同一手远近变化 |
| 2 有效关节角度 | 静态 P95 朝向误差 ≤ 15° | 可测朝向参考夹具；仅统计声明有效的朝向，另报有效率 |
| 2 覆盖/质量 | 目标测试条件下位置有效率 ≥ 90%；退化姿态不强行给有效旋转 | 同时报失败率/估算比例，避免只挑有效样本报误差 |
| 3 丢失/恢复 | 最后有效帧过期后 ≤ 200 ms 标为丢失；重现后 ≤ 500 ms 恢复 | 遮挡/USB 重连/切后台，记录单调时钟；USB 恢复另报设备开启时间 |
| 3 吞吐 | 单手 ≥ 20 FPS、双手 ≥ 18 FPS（每次持续 5 min）；稳定 30 FPS 为后续优化目标 | 全程平均及 P95 帧间隔、帧年龄；注明温度/曝光/分辨率 |
| 3 延迟 | 接收至 Provider 发布 P95 ≤ 100 ms；曝光至显示指标待测 | 同一主机时钟域测前者；后者需曝光时间戳或高速摄影，不能拿 -1 推算 |
| 3 稳定性 | 30 min，20 次场景往返/断连恢复，无重复子系统或持续资源增长 | 日志、子系统实例计数、内存/GPU 资源采样 |
| 4 交互 | 100 次有意捏合成功率 ≥ 95%；5 min 无意选择 ≤ 1 次；丢手后无残留选择 | 射线/UI 优先，逐次记录；近抓取另按阶段 2 的真实精度决定 |

阶段 0 只能验收第一行及契约校验，不能提前宣称其他行完成。
精确近距离抓取、头部平移跟踪、光学最终对位、真实曝光至显示延迟仍未交付。

## 6. 可重复验证与结果

```powershell
dotnet run --project tests/CoreChecks/CoreChecks.csproj
./tools/ValidateXRHandsStage0.ps1 -UnityPath 'D:\unity\hub\editor\2022.3.14f1\Editor\Unity.exe' -IncludeCardboard
```

脚本每次在被忽略的 `build/validation/xr-hands-stage0-<随机标识>/` 创建两个全新工程：

1. `sdk-only`：只导入基础 SDK，检查没有引入 XR Hands。
2. `with-xr-hands`：导入配套包和固定版本；可加本地 Cardboard，编译适配与现有显示程序集。

执行 `Stage0CompileProbe.Run` 检查 26 个标准关节映射、21 个输入索引、5 个缺失点及左右手映射，
并确认阶段 0 未创建手部子系统。成功日志含 `SPATIALAI_STAGE0_PASS`；脚本额外核对锁文件的顶层版本。
初次执行需要 Unity 授权与包下载网络；若未准备本地 Cardboard，省略 `-IncludeCardboard`，
这时不能宣称验证了 Cardboard 共存。
验证只在编辑器内进行，不构建/安装 APK，也不改手机上的当前手势流程。

本轮结果已记入 [验证记录](VALIDATION.md)。
本轮 CoreChecks 458 项通过；两个严格最小工程在 Unity 2022.3.14f1 编译及映射检查通过，
包含 Cardboard/UGUI 共存配置与不安装 Cardboard 的独立适配包配置。
空间误差、真实延迟、恢复与交互目标尚未实测。
Unity 官方 API/Provider 说明：
[XR Hands 1.4](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.4/manual/index.html)、
[XRHandJointID](https://docs.unity3d.com/Packages/com.unity.xr.hands@1.4/api/UnityEngine.XR.Hands.XRHandJointID.html)。

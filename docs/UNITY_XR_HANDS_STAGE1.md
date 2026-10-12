# Unity XR Hands 阶段 1：模拟手与标准 Provider

更新日期：2026-10-09。本阶段提供可运行的模拟三维手和标准 XR Hands 接口。
真实 UVC 空间转换、单目深度与镜片对位尚未实现；现有手机 APK 未改动。

## 已交付

| 文件/入口 | 作用 |
| --- | --- |
| `packages/com.spatialai.xr-hands/Runtime/SpatialHandSubsystem.cs` | 注册 `SpatialAI-Simulated-Hands` descriptor，提供 Provider、26 点布局、逐项状态与标准更新标记 |
| `SimulatedHandsFrameSource.cs` | 独立左右手的确定性空间帧；张掌/握拳/捏合、刚体移动/旋转、丢失与缺失关节 |
| `SpatialHandsSimulator.cs` | 唯一模拟组件所有者、动态/渲染阶段更新、启停销毁及快照复用 |
| `SpatialXRHandsVisualizer.cs` | 只通过标准 XR Hands API 读取关节的可视化消费示例 |
| `SpatialHandsSimulationControls.cs` | 键盘或 Inspector 控制 |
| `Spatial AI → XR Hands → Create Simulation Scene` | 创建/打开可运行场景；保留已存在的同名文件 |
| Package Manager → Hands Simulation | 可导入的场景 sample |
| `tools/ValidateXRHandsStage1.ps1` | 全新隔离工程中的确定性检查与 Play/场景生命周期验证 |

基础 SDK 仍不依赖 XR Hands。配套包固定阶段 0 的 Unity 包版本，另明确依赖 Physics 模块 1.0.0，
用于调试关节球体的 Primitive/Collider；具体依赖与坐标定义见 [阶段 0](UNITY_XR_HANDS_STAGE0.md)。

## 运行与人工检查

1. 工程 manifest 指向本地基础 SDK 和配套包，安装示例见 [配套包 README](../packages/com.spatialai.xr-hands/README.md)。
2. 使用菜单创建 `Assets/SpatialHandsSimulation.unity`，或从 Package Manager 导入场景 sample 后打开。
3. 点 Play，左手青色、右手橙色。点击 Game 窗口后使用 `1/2/3` 切换张掌/握拳/捏合，
   `L/R` 切换对应手的跟踪，空格暂停/恢复运动。
4. 暂停运动，在 Inspector 单独改变左手/右手的 `Position` 与 `Euler Angles`。
   检查只有目标手移动；旋转时整个骨架随根姿态旋转，骨长不变化。
5. 选择 `Missing Joint = Palm`，手掌点及相连线应隐藏；选择 `Missing Rotation = IndexTip`，
   该指尖没有可读的标准 Pose。恢复为 `Count` 后恢复显示。
6. 关闭单手 `Tracked`，该手全部消失，另一只手继续显示；再次勾选后恢复。
7. Play/Stop、禁用/启用模拟组件、重新打开场景，确认只出现一套手部数据且停止后没有残影。

此轮不需要手机、UVC、模型或 Android AAR。人工检查用于确认使用体验，
不能据此验收摄像头精度、现实空间尺度或镜片光学对位。
模拟骨架采用简化尺寸和 FABRIK 链求解，能保持骨长；不代表真实人的解剖模型或手势分类精度。

## 标准 API 与状态规则

普通消费脚本通过 `SubsystemManager.GetSubsystems<XRHandSubsystem>` 找到
descriptor ID 为 `SpatialAI-Simulated-Hands` 的运行实例，再读取 `leftHand`、`rightHand`、
`rootPose`、`GetJoint` / `TryGetPose`，并订阅 `updatedHands`、`trackingAcquired`、`trackingLost`。
`SpatialXRHandsVisualizer` 即这种消费者，可替换为用户自己的消费者。
Unity 官方 HandVisualizer sample 也可用于进一步检查；本轮已运行的可视化是 SDK 提供的标准 API 消费示例。

- 布局为 26 个可尝试跟踪的关节。每帧缺失点是暂时无效，不能改成永久不支持的 `WillNeverBeValid`。
- XR Hands 1.4 的 Pose 是整体有效位，必须同时有位置和旋转；只有位置时不伪造单位旋转。
- 根姿态无效或整手丢失时，清空该手所有关节有效性，并取消对应 Root/Joints 更新成功位。
- 单个关节丢失时，手仍可跟踪，只有该关节 `TryGetPose` 返回 false；半径状态单独处理。
- 模拟源支持 Pose 与 6 mm 半径，不声明速度。所有姿态为米制 tracking origin 数据。
- 动态阶段产生新序号，渲染阶段复用同一快照；不将两次回调计为两帧识别结果。
- 无 XR Display 的编辑器可能不发 `onBeforeRender`，因此桌面模拟用 LateUpdate 补足渲染阶段；
  有运行中的 XR Display 时使用真正的 `onBeforeRender`，每个阶段按帧号去重。
- descriptor 注册可重复调用但不重复登记；组件发现已有同类子系统时拒绝启动第二个。
  禁用/销毁只释放自己拥有的实例，不停止其他 XR Provider。

当前启动入口是模拟组件的显式所有权，不是 XR Plug-in Management 中的生产设备 Loader。
Cardboard 的显示与头部输入 Loader 保持原职责；真实手部来源及发布配置在阶段 3/5 处理。
`SpatialHandSubsystem.FrameSource` 必须在停止时修改；本阶段只接受 `Source=Simulation`。
`LastFrame` 保留 SDK 时间、来源和估算标记，因为标准 XR Hands 不直接暴露这些附加字段。
数据过期、摄像头后台恢复、跨时钟域对齐与性能统计留到阶段 3，不将模拟快照缓存当作相机恢复实现。

## 自动验证

```powershell
dotnet run --project tests/CoreChecks/CoreChecks.csproj
./tools/ValidateXRHandsStage0.ps1 -UnityPath 'D:\unity\hub\editor\2022.3.14f1\Editor\Unity.exe' -IncludeCardboard
./tools/ValidateXRHandsStage1.ps1 -UnityPath 'D:\unity\hub\editor\2022.3.14f1\Editor\Unity.exe' -Graphics
```

阶段 1 脚本创建被 Git 忽略的 `build/validation/xr-hands-stage1-<标识>/`：

- 确定性测试检查 descriptor 唯一、26 点标准布局/索引/手别、米制根姿态和半径、未支持速度、
  组合刚体变换、骨长、捏合、缺失关节/旋转、丢失/恢复、停止后的无效状态与归零版本。
- 进入两次 Play，各进行 20 次组件启停和 10 次真实场景重载；验证子系统数量及自动更新回调。
- 图形模式额外渲染 `simulation-preview.png`，便于检查场景布局。
- 日志必须含 `SPATIALAI_STAGE1_PASS`，否则工具返回失败；工程内保存的是没有测试探针的示例场景。

省略 `-Graphics` 可运行无图形验证，不生成图片；真实 XR Display 的硬件回调时序仍需后续真机测试。
本轮结果与证据目录见 [验证记录](VALIDATION.md)。未构建或安装新 APK，未提交 Git。

下一阶段优先完成 UVC 内参/安装外参、单目尺度与根位置求解、21→26 点转换及误差测量，
再将真实来源接入阶段 3 的 Provider 更新与恢复链路。

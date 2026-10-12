# Unity XR Hands 阶段 4：3DOF 食指 UI 与标准手势

## 当前方案（2026-10-12）

依据最新需求，保留两个方块和模拟手骨架，食指附近用圆圈交互；移除可见射线与移动白球。本文后续射线及“移除骨架/方块”的记录为历史实验，以本节为准。
圆圈位置参照 `456.mp4`：使用拇指尖与食指尖中点，保留 X 镜面补偿，沿视线投影到固定深度 0.72 × 显示倍率（默认 2.16 单位）。原有圆环网格、尺寸、材质与颜色逻辑不变；捏合不冻结指针，指尖中点与整手移动均实时更新。
方块中心位于相机局部左右 ±0.65、高 0.1、前方 4.2 单位；UI 按钮高 -1.15、前方 4.2 单位。方块和按钮都跟随 XR 相机，转头后保持视野位置，适合当前 3DOF 交互。
调试面板放在视野中部、方块下方：顶边高 -0.12，底边 -0.54，深度 2.8 单位；深色背景与文字比方块近表面 3.975 更靠近相机，避免被方块遮挡，且不参与输入命中。OPEN/FIST/PINCH/NONE 直接合并在左右手状态文字中，隐藏按钮式手势图块。新增 AI/UVC/Render FPS 行。
当前默认 CPU / VIDEO；广播用 `--es inferenceDelegate GPU/CPU` 调试切换。侧掌与短暂漏检补偿统一在 SDK，自带可视化自动处理；保留骨架不冒充标准跟踪，不参与交互。旧场景也已共用此流程，详见 [SDK 补偿与后端配置](UVC_HAND_SDK_COMPENSATION.md)。

应用层只使用 Unity 标准接口，不增加 Spatial 专有手势事件或语义枚举：

| 能力 | 应用接入 |
| --- | --- |
| 显示、3DOF | Unity XR、Cardboard XR Provider |
| 手部数据、跟踪状态 | `XRHandSubsystem`、`XRHandJoint`、`XRHandTrackingEvents` |
| 手势定义 | `XRHandShape` / `XRHandPose` |
| 手势开始、结束 | 官方 Gestures 示例 `StaticHandGesture.gesturePerformed` / `gestureEnded` |
| UI 点击 | `Button.onClick`，配合标准 XRI UI 组件 |
| 方块交互 | `XRSimpleInteractable` 的 hover/select 事件 |

Unity XR Hands 1.4.3 没有固定的“握拳/张开/捏合”事件枚举。此处使用标准形状资产定义 OPEN/FIST/PINCH，由原样导入的官方组件识别。SDK 不把 MediaPipe 分类名转为应用事件。
OPEN 约束四指 FullCurl ≤ 0.35 且 BaseCurl ≤ 0.25；FIST 约束四指 BaseCurl ≥ 0.6 且 FullCurl ≥ 0.25；PINCH 使用食指标准 Pinch ≥ 0.75。阈值是可编辑示例配置，真实 UVC 的估算关节仍需佩戴调参和验收。
持续匹配至少 0.2 秒触发一次，持续保持不重复触发。官方组件在跟踪丢失时不保证触发 `gestureEnded`，持续动作的取消应另绑定标准 `XRHandTrackingEvents.trackingLost`；示例用此事件清除手势文字状态。

接入步骤：先导入 XR Hands 官方 **Gestures** 样例（Package Manager 或 **Spatial AI > XR Interaction > Import Unity XR Hands Gestures**），等待编译，再生成 UVC/Simulation Interaction Scene。官方样例需要 TextMeshPro，本可选包声明 3.0.6 依赖。
生成 `XRHandsStandardGestures.prefab` 和三个 `XRHandShape` 资产；开发者在 Inspector 绑定官方组件 UnityEvents 到自己的操作，按钮直接绑定 On Click。
已存在的用户资产不覆盖。仅导入本包 Samples 场景不会自动导入官方手势源码，需要上述菜单生成完整场景。
更多操作见 [包 README](../packages/com.spatialai.xr-interaction/README.md) 与 [Unity 官方组件说明](https://docs.unity.cn/Packages/com.unity.xr.hands%401.4/manual/gestures/static-hand-gesture.html)。

内部保留标准 `ActionBasedController` / `XRRayInteractor` 执行对象与 UI 命中，不绘制线条，保留正常 3D interactionLayers。
不能把 `raycastMask` 置零来禁用 3D：XRI 会把该层掩码传给 UI，导致按钮也无法命中。
SDK 适配内部保留原有迟滞、50ms 去抖和失败恢复：松手触发一次点击，丢失取消不点击，恢复先张开。

### 人工验收

1. 左右手骨架、两指之间的圆圈及移动方向正确，两个方块保留；没有射线或白球。确认圆圈位置符合 `456.mp4`，圆环样式不变；捏合与整手移动时标记继续更新。
2. 将圆圈移到按钮，悬停变白，捏合变绿，正常松开计数只增加一次；保持捏合不重复点击。
3. 捏合时移出手，取消且不增加计数；返回时先张开，才可重新点击。
4. 左右手分别张开、握拳、捏合并保持至少 0.2 秒，调试文字显示对应状态；不出现像按钮的手势行。
5. 转头后方块、按钮跟随眼睛，保持视野位置；调试面板在中部偏下、文字不被方块遮挡。检查镜片中的合相、可读性与舒适度。
6. 圆圈对准方块悬停变亮、捏合变白、松开恢复；手移出时取消选择，无残留。

首次 USB 授权、重插权限 UVC-PERM-001 和精确相机标定继续搁置；没有参数仅提示。

## 历史射线实现与验证

日期：2026-10-10。交付独立可选包 `com.spatialai.xr-interaction`，依赖 XR Hands 包，不改变基础显示/3DoF 用法。
UVC 首次使用及 USB 重插的权限确认问题 UVC-PERM-001 继续暂缓，本阶段未修改 Android 授权流程。

## 交付与依赖

固定 Unity 2022.3.14f1c1、XR Hands 1.4.3、Input System 1.7.0、XRI 2.5.4、uGUI 1.0.0。
版本依据本机官方包源码及编译验证；XRI 的 [ActionBasedController 文档](https://docs.unity.cn/Packages/com.unity.xr.interaction.toolkit%402.5/api/UnityEngine.XR.Interaction.Toolkit.ActionBasedController.html)说明输入动作与标准交互状态的对应关系。

- `SpatialHandInputDevice`：左右两个独立 Input System 设备，通过 LeftHand/RightHand usage 绑定。
- `SpatialHandInteraction`：从标准 XR Hands 关节生成远距离射线和捏合状态，管理失效、重置及设备生命周期。
- `SpatialPinchGate`：按新源帧判定，加入迟滞、去抖与恢复后重新武装。
- `SpatialHandRayBinding`：普通 ActionBasedController、XRRayInteractor、XRInteractorLineVisual；无需 OpenXR/Meta 控制器布局。
- 可编辑的 `SpatialHandActions.inputactions` 与 `SpatialHandInteractionRig.prefab`。
- UVC 与模拟交互场景，普通 XRSimpleInteractable 对象、世界空间 Canvas、Button、XRUIInputModule 和 TrackedDeviceGraphicRaycaster。
- 自动化工具 `tools/ValidateXRHandsStage4.ps1`，验证真正的 XRI 选择事件及 UI 点击事件。

## 输入与坐标

| 控制 | 类型 | XRI 用途 |
| --- | --- | --- |
| devicePosition | Vector3 | 射线起点 |
| deviceRotation | Quaternion | 射线方向 |
| isTracked | Button | 必要关节有效 |
| trackingState | Integer | 有效时 Position/Rotation，失效时 0 |
| pinch | Button | Select 与 UI Press，共用去抖后的状态 |
| pinchStrength | Axis | 诊断强度，不直接替代 Select |

绑定格式如 `<SpatialHandInputDevice>{LeftHand}/pinch`；对应 Action Map 为 LeftHand/RightHand。
可直接复用 prefab，或把这些动作绑定到自己的 ActionBasedController。使用标准 XRInteractionManager 和可交互组件。
不将 MediaPipe 的 None/OpenPalm 等类别当作 Select/Activate；当前没有 Activate、滚轮、震动、poke 或近距离抓取输入。

射线从食指/小指近节关节的中点发出，方向为虚拟肩部指向该掌部锚点。
`aimMode` 可选择 `ShoulderToPalm` 或 `HeadToPalm`；SDK prefab 默认前者。
UVC 手机交互示例现用后者，让掌部在视野中的位置对应前方目标，减少未标定肩部偏移带来的瞄准困难。
虚拟肩部相对物理头部为左右 ±0.16m、下方 0.18m、后方 0.04m；这是可用性启发式，不是已标定的身体跟踪。
镜像预览先把掌部和头部映射到显示坐标，再按最终手别确定同侧虚拟肩；左右肩不会随 X 反射交换。
掌部锚点避免仅因拇指/食指闭合就大幅改变射线；必要关节缺失、掌宽无效或射线指向头部后方时取消交互。

正常 SDK 使用：prefab 父节点与标准关节的 tracking origin 一致，prefab 根变换保持单位变换；
需要物理头姿时配置 bridge.physicalHead 与 trackingOrigin，保持与真实 Provider 的物理头姿来源一致。
默认展示距离倍率为 1、无 X 反射，关节数据本身不修改。

手机示例沿用阶段 3 的头部局部预览：对象、UI、骨架和射线展示都随相机，骨架/射线展示倍率为 3，
按 Host 配置补偿 X 反射。示例动作中的射线姿态是展示父节点坐标中的 aim pose；
不能把它当作未放大的真实手部位置，也不能把示例当作固定世界或近距离精确抓取验收。

## 捏合与取消

捏合比值为拇指尖/食指尖距离除以尺度参考：取食指/小指近节关节间距与腕部到中指近节距离 ×0.875 中较大者。
第二个当前帧尺度参考用于减少捏合时掌宽收缩对阈值的影响。
默认按下阈值 0.32，松开阈值 0.48，两种转换都至少持续 50ms。
按识别新源帧判定，实际持续时间受识别帧率影响；重复 Dynamic/BeforeRender 快照不重复计数。
输入事件由下一次正常 Input System 更新消费，最多增加一个常规输入更新周期，不在每次回调强制全局更新。

首次取得跟踪、丢失后恢复、换源、归零、切换射线绑定或组件重启后，需先张开手持续 50ms，再允许选择。
未武装时 HUD 显示 `open to arm`，射线暂不注册交互。这样也避免旧 UI Press 在设备重建时触发误点击。
StateChange 模式下，一次捏合只开始一次对象选择，松手释放；UI 在正常松开时点击。

关节失效、无手、超时、识别关闭、USB 断开、组件禁用、暂停或输入源改变时，取消标准选择和 hover，
关闭射线并发布中性输入；UI 按压随射线注销取消，不把跟踪丢失当作按钮点击。
禁用/销毁桥接释放其两个输入设备；其他输入设备不受管理。
标准 UVC Provider 默认掌轴比限制仍为 1.6；本交互示例可配置为 4，只接收通过其他边界检查的当前估算数据。
关键点缺失、严重退化、范围错误或超时仍会失效；旧预览的 depth-held/missing-held 不用于选择。

## 使用

1. 在基础 SDK、Cardboard 与 XR Hands 包之外，通过 Add package from disk 导入 `packages/com.spatialai.xr-interaction/package.json`。
2. Player Settings 的 Active Input Handling 选择 Input System Package 或 Both；手机继续配置现有 Cardboard XR Loader。
3. 选择 **Spatial AI > XR Interaction > Create UVC Interaction Scene**；加入 Build Settings。
4. 菜单生成 `Assets/Scenes/SpatialHandInteraction.unity`、`Assets/SpatialAI/Prefabs/SpatialHandInteractionRig.prefab`
   和 `Assets/SpatialAI/Input/SpatialHandActions.inputactions`。已有文件不覆盖，便于自行修改。
5. 编辑器无摄像头验证选择 **Create Simulation Interaction Scene**；1 张开、3 捏合、L/R 切换跟踪，可在 Inspector 调整模拟手位置和姿态。
6. 示例 APK 使用广播切换，保留旧 hands/xrhands 场景：

```powershell
adb -s 10.168.2.104:5555 shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene xri --ez gestures true --ei exposureMode 1 --ei exposureTimeUs 16600
```

先张开手，使掌部在视野中移到目标上。上方对象悬停时变亮，捏合选中时变白，松开后保留悬停颜色，移开后恢复原色；
下方两个 UI 按钮松开后增加 HUD 的 L/R 点击计数。左右手可分别操作，颜色沿用左青/右橙。
日志关键字 `SpatialXRI`，记录捏合边沿、目标选择/释放、取消原因与 UI 点击次数。
性能与输入时序继续看 `SpatialXRHands`，日志标签仍为 Unity。

## 验证与后续

合成检查覆盖阈值边界/迟滞、首次闭合阻止选择、真正的动作绑定、标准 XRI 选择、重复渲染不重复触发，
跟踪丢失/缺关节/归零/输入模式切换取消、双手同选、真实 UI 点击、丢失/归零取消 UI 按压而不点击，
20 次设备启停、前后台、prefab 动作资产加载及示例组件启停/销毁。
要求运行时没有错误或异常；这些结果不代替真实捏合与目标瞄准验收。
全新工程验证通过：39 项契约检查、54 项 XRI 集成检查、20 次设备启停，合成输入触发 3 次真实 UI 点击。
日志：`build/validation/xr-hands-stage4-9acb9a85afb44fdfb4a318ba25f6252e/verification.log`。
包括导入 UPM 示例 prefab 后解析动作资产，以及运行时无错误/异常检查。

最终 APK：`build/samples/android/spatialxr-xri-stage4.apk`，55,592,213 字节，v2 签名校验通过。
SHA-256：`a9286706c210cb270baf10b7ec9a7bbaa84d23f488d88d346e8f239905f092c5`。
构建日志：`build/logs/xr-hands-stage4-layout-apk.log`；已安装到 `10.168.2.104:5555`。
无摄像头启动时交互场景显示对象、按钮和状态文字，双手保持 untracked，等待外置 UVC 相机。
XR Hands → XRI → IMU → XRI 场景往返及前后台恢复检查通过；本应用进程记录未发现 Unity 错误或异常。
截图：`build/logs/xri-stage4-final-screen.png`；运行记录：`build/logs/xr-hands-stage4-final-device.log`。
用户明确暂不连接摄像头，因此真实瞄准、捏合、点击、取消与恢复交互保留待验收；本阶段软件实现完成，实机交互验收尚未完成。

### 同日接回 UVC 后的补充检查

用户随后接回 HXQ；采集恢复到约 30 FPS，640×480，手动曝光 16.6ms，VIDEO/GPU 识别约 19～23 FPS，稳定渲染约 60 FPS。
识别开关、XR Hands/IMU/XRI 场景往返及前后台后恢复真实输入检查通过；启动/恢复窗口的帧率会暂时降低，不能当作稳定性能结论。
原版真实日志记录右手捏合、Right object 选择及正常释放，也记录严格几何失效后取消。
日志：`build/logs/xri-stage4-uvc-device.log`、`build/logs/xri-stage4-ray-user.log`。
用户报告可见射线从手部向眼睛延伸，与手掌朝向无关；因此不能据目标选择事件宣布方向验收通过。

统一 `XRRayInteractor.rayOriginTransform` 为控制器 Transform，关闭 Line Visual 独立起点覆盖；
可见线与射线启停同步，跟踪丢失时立即隐藏旧线条。
新增每两秒 `SpatialXRI aim` 日志：展示坐标中的 origin/direction、cameraForwardDot、visualMatchesRay 和 selected。
cameraForwardDot 为正表示实际控制器射线与相机前向夹角小于 90°；显示舒适度和镜片中观感仍需人工确认。
此修改未反转 Z 轴；原射线计算保持虚拟肩部指向掌部，避免在证据不足时将实际检测射线反转。

修订版验证通过 39 项契约、57 项集成及 20 次启停，新增可见线与检测同源、前向及失效隐藏检查。
日志：`build/validation/xr-hands-stage4-cda259d54df3467798530dd1381a4a82/verification.log`。
诊断 APK：`build/samples/android/spatialxr-xri-ray-diagnostics.apk`，55,592,529 字节，v2 签名通过，已安装。
SHA-256：`9e9c3b5b299cf513c0da20eb590d6122eff531621a567b774c65c5809a9e73b3`。
构建日志：`build/logs/xri-stage4-ray-diagnostics-apk.log`；运行日志：`build/logs/xri-stage4-ray-diagnostics-device.log`。
真实射线方向问题复测、左右/双手完整选择、UI 点击、按压时丢失取消及恢复重新武装仍待验收。

### 镜像展示的肩部偏移修正

用户复测：左手射线朝眼睛且偏左，右手朝眼睛且偏右。
方向诊断 `build/logs/xri-stage4-ray-direction-feedback.log` 显示检测射线前向点积约 0.94～0.96，
同时左右射线向外撇。代码确认原算法在按手别选择肩部后整体做 X 反射，将左肩翻到右侧、右肩翻到左侧。
改为先反射掌部与头部，再按最终手别确定同侧肩部；保持原始 XR Hands 数据和捏合距离不变。
明确 LineRenderer 使用世界坐标，日志增加实际渲染线段 start/end/forwardDot，以区别检测朝向和最终可见线段。
39 项契约、62 项集成与 20 次启停检查通过，新增镜像左右手、三倍展示距离和同侧肩部前向检查。
日志：`build/validation/xr-hands-stage4-d6e1da7d11f246039494dd359d959378/verification.log`。
此修正纠正代码中的肩部左右交换；镜片中的朝向观感及完整交互仍需重新人工确认。
修正版 APK：`build/samples/android/spatialxr-xri-reflected-shoulder.apk`，55,596,021 字节，v2 签名通过，已安装到 `10.168.2.104:5555`。
SHA-256：`dc1155ae7e69b0ff734b7f253a3b61cd308300d4b801095f399d3cf371641882`。
构建日志：`build/logs/xri-stage4-reflected-shoulder-apk.log`；运行日志：`build/logs/xri-stage4-reflected-shoulder-device.log`。
启动后已进入 XRI 场景，手动曝光 16.6ms，真实采集恢复约 30 FPS；等待用户张开手复测射线。

### 方向提示与后续复测

用户确认肩部修正后仍看起来朝眼睛延伸，单眼观察也存在；不能把肩部修正认定为该视觉问题已解决。
最终渲染线段日志显示实际向前：例如 start.z=1.825、end.z=11.547，前向点积 0.972。
因此保留真实射线方向，增加终点箭头及沿实际显示线段从手向终点移动的白色箭头，帮助辨认方向；
箭头无碰撞体，不参与选择，失效时随线条立即隐藏。此为方向提示，不代表光学已校准。
39 项契约、70 项集成和 20 次启停通过，包含基于标准 XRI 端点的箭头朝向与取消隐藏检查。
验证：`build/validation/xr-hands-stage4-852433af61b54635b4840273119f0998/verification.log`。
APK：`build/samples/android/spatialxr-xri-direction-cues.apk`，55,595,461 字节，v2 签名通过，已安装。
SHA-256：`e98157896bb5eb40556a411328e9a24325e8021bbf88609e7bae2b0314b470f0`。
构建：`build/logs/xri-stage4-direction-cues-apk.log`；运行：`build/logs/xri-stage4-direction-cues-device.log`。
已进入 XRI 场景、真实采集约 30 FPS；箭头在镜片中的运动方向及完整交互仍待用户确认。

### 用户对方向观感的进一步确认

用户最初反馈移动箭头仍像朝眼睛运动，随后直接观察手机屏幕，反馈屏幕里的射线方向似乎正确，镜片中更像视觉错觉。
该反馈与检测/渲染端点向前的日志一致；当前证据更支持镜片中的方向感问题，具体光学原因未确定。
保持当前前向射线，不反转 Z 轴。镜片中的深度/方向感纳入已有小米光学与尺度待验收项，
不能以此认定光学已校准，也不把左右肩修正单独解释为光学问题的解决。
可继续功能验收：左右/双手选择与释放、UI 每次松手一次点击、按压时手移出取消且恢复需先张开。
当前截图：`build/logs/xri-stage4-live-arrows.png`；完整交互仍未得到用户确认。

### 捏合失效与掌部瞄准优化

用户反馈目标难选中、捏合时手消失并显示 tracking lost。
`build/logs/xri-stage4-pinch-loss.log` 显示 MediaPipe 仍输出 hands=2，而 SDK 频繁因掌宽/掌长估算比超过 1.6 拒绝手部。
不能把这些事件全部解释为摄像头漏检；同时仍可能存在真实漏检或无效关节。

- 新增 `maximumPalmAxisRatio`，默认 1.6，交互示例明确设为 4。用当前投影中较少缩短的轴估算深度，
  所有输出仍为 MonocularEstimate；缺锚点、掌部塌缩、比值超过 4、范围错误及过期仍拒绝。
  source 重建保留该策略；日志带 `foreshortened accepted`，没有旧帧保持或虚构精确跟踪。
- 捏合尺度增加当前腕部/中指参考，避免掌宽压缩导致捏合阈值随动作大幅变化。
- UVC 示例改为 HeadToPalm，掌部在画面中的位置对准目标；方块悬停变亮，选中变白，松开保留悬停。
  可编辑 prefab 和模拟场景的默认 ShoulderToPalm 保留，原始关节不受显示倍率、反射或瞄准模式修改。

982 项核心检查通过；标准 XR Hands 的 96 项契约和 20 次启停回归通过。
阶段 4 的 46 项契约、74 项交互和 20 次启停通过，新增当前帧宽容估算、失效清理、过期及实际悬停/选择/释放路径。
日志：`build/validation/xr-hands-stage3-1ae652b5ed5e49ca98bb05728781013c/verification.log`、
`build/validation/xr-hands-stage4-bbb656d1da624565a50b56dfad8d409b/verification.log`。
掌部瞄准与捏合保持的改善仍需本轮真实输入复测，不能以合成结果替代。
APK：`build/samples/android/spatialxr-xri-palm-pointer.apk`，55,788,069 字节，v2 签名通过，已安装到 `10.168.2.104:5555`。
SHA-256：`e7c8fc376cc12ae9b55286fc1790ff749e3ff7980d5fbd6b334ee491a208f9c6`。
构建：`build/logs/xri-stage4-palm-pointer-apk.log`；运行：`build/logs/xri-stage4-palm-pointer-device.log`。
实机日志确认 `aim=HeadToPalm`、`maxPalmAxisRatio=4`，采集恢复约 30 FPS，手动曝光 16.6ms。
已看到轴比 2.056 的新鲜右手数据被标为 estimated 并正常 acquired；完整捏合保持仍待本轮确认。

用户复测反馈：捏合基本不再丢失；白色瞄准标记与方块重叠时，捏合一般可以选中。
真实日志记录 Left object 多次 hover/select，以及 canceled=False 的正常 release；缺锚点、重复手别及轴比超过 4 的失效仍可出现。
该反馈确认本轮捏合稳定性改善与基本目标选择，不替代双手完整操作、UI 点击和失效恢复验收。
HeadToPalm 模式中射线与观察方向接近重合，线段投影缩短，原方向箭头呈白色圆点；当前是掌部光标式瞄准的表现。
捏合按归一化距离阈值触发（0.32/0.48），不是指尖接触检测；本轮反馈未提出触发时机问题。

2026-10-10 食指射线调整：增加 `SpatialHandRayOrigin`，组件默认 `Palm`，UVC 示例使用 `IndexTip`。
实际 XRI 碰撞起点和可见线段起点均移到当前食指尖，沿用骨架的显示反射补偿和距离倍率 3。
方向继续由头部到掌部的向量决定，避免捏合时食指弯曲直接旋转射线；捏合阈值和跟踪失效取消规则保持原样。
食指尖随捏合移动仍可能平移射线，需实机确认瞄准及 UI 松手是否稳定；镜片中的方向感是否改善也需用户验收。
本轮编辑器验证通过：46 项契约、83 项集成检查、20 次生命周期循环和 3 次模拟 UI 点击。
新增检查涵盖双手食指尖的镜像/缩放起点、保持掌部方向、实际碰撞悬停/捏合选择，以及食指尖缺失立即取消。
验证日志：`build/validation/xr-hands-stage4-714bfc958246468b86cbadcbf29b4955/verification.log`。
APK：`build/samples/android/spatialxr-xri-index-ray.apk`，SHA256 `472aca0efa4ebc0fb1031974a8cbaa31c202ea627ca15ab28744fc9ca4d56a97`，v2 签名验证通过。
已安装至 `10.168.2.104:5555` 并切换 `xri`；应用日志确认 `aim=HeadToPalm rayOrigin=IndexTip`，手动曝光 16.6ms 回读匹配。
运行日志：`build/logs/xri-stage4-index-ray-device.log`；上述启动确认不替代镜片观察和实际操作验收。

2026-10-10 视频复核与方向修正：用户的 `123.mp4` 显示白色标记在画面中由指尖向掌部/手腕移动。
只移动起点、继续使用 HeadToPalm 会让远端趋向掌部的投影位置，不能仅凭世界坐标 forwardDot 为正认定视觉方向正确。
UVC 示例改用 `IndexExtension`：以当前射线起点相对头部的向量为基础，加上手腕到食指根部在头部视平面中的向量乘以 2.5。
这样在估算深度向前的同时，线段在画面中越过指尖向外延伸；这仍是单目估算瞄准，不是精确食指三维指向。
捏合关闭阈值区间及按下/松开去抖期间保持最后一次张开手时的方向，当前指尖位置继续更新；张开并完成释放后恢复方向更新。
失去跟踪、缺少手腕/指尖、来源或归零变化、暂停和停用均清除保持方向，恢复时仍要求先张开手。
`SpatialXRI aim` 日志增加 `aimMode` 与 `aimHeld`，用于区分新方向和捏合保持状态。
验证通过：46 项契约、99 项集成检查、20 次生命周期循环、4 次模拟 UI 点击；包括双手实际碰撞线及移动箭头的屏幕延伸方向、前向深度、捏合旋转保持、缺手腕取消与闭手恢复阻止。
验证日志：`build/validation/xr-hands-stage4-aca131f1084146d7a4e71e4fa82dc0f7/verification.log`。
APK：`build/samples/android/spatialxr-xri-index-extension.apk`，SHA256 `c07901fab9aa8f66effe3a635708f19a31d4b14e8e1779c15a967358d5212019`，v2 签名验证通过。
已安装至 `10.168.2.104:5555` 并切换交互场景，日志确认 `aim=IndexExtension rayOrigin=IndexTip`；镜片观察及真实操作仍待人工验收。
运行日志：`build/logs/xri-stage4-index-extension-device.log`。
本次启动 UVC 首次打开后关闭并再次请求 USB 授权，尚未取得持续采集日志；需确认现有授权/重连后再人工验证，属于已记录的 USB 权限恢复待办。

2026-10-10 用户复测：射线终点稳定，起点跟随指尖，捏合选择稳定；但白球由小变大，透过镜片仍像向眼睛靠近。
复核发现移动标记原先按距离放大，且较大的静止终点也是白色，会使远去的移动标记与白色终点混淆。
本轮保留 IndexExtension 碰撞射线及捏合保持逻辑，移动标记半径固定为 0.045 Unity 单位，不再根据距离放大；终点也采用固定尺寸，并改为射线颜色。
预期：白色移动标记从指尖沿线向远端运动时，由透视投影自然缩小，彩色终点保持静止。镜片中的最终深度感仍需人工复测。
本轮验证通过 46 项契约、105 项集成检查、20 次生命周期循环、4 次模拟 UI 点击。新增双手检查：实际标记网格的远端投影小于近端、改变射线长度不放大标记、静止终点与移动白色标记颜色不同。
验证日志：`build/validation/xr-hands-stage4-f3e2b6701a0f45f787087eede951a7b8/verification.log`。
APK：`build/samples/android/spatialxr-xri-marker-perspective.apk`，SHA256 `96da5d91a6e3035702d9e92d66cb62285edc6859bc9ca5f59e31501fd465a4b6`，v2 签名验证通过。
已安装至 `10.168.2.104:5555`，交互场景启动确认 `aim=IndexExtension rayOrigin=IndexTip`；运行日志 `build/logs/xri-stage4-marker-perspective-device.log`。

2026-10-12 掌部起点与压低角度：按用户要求，UVC 示例改为 `rayOrigin=Palm`、`aimMode=HeadToPalm`。
在头部到掌部的方向上应用 `aimPitchOffsetDegrees=-15`，并设置 `maximumAimElevationDegrees=0`，使射线在画面中向较低位置延伸。
新增 `holdAimDuringPinch` 并在 UVC 示例启用，捏合保持方向、丢失取消和先张开重新武装保持有效；起点跟随掌部，白色移动标记仍使用固定尺寸。
组件默认偏移为 0°、仰角上限 90°、可选方向保持关闭；模拟场景行为保留原样，IndexExtension 原有保持逻辑仍有效。
最终验证日志：`build/validation/xr-hands-stage4-2d66bc1e9b8446679ddff477b0de22a1/verification.log`，掌部镜像/缩放起点、仰角上限、负偏移、屏幕向下延伸、捏合保持选择及释放均通过。
本轮共通过 46 项契约、111 项集成检查、20 次生命周期循环、4 次模拟 UI 点击。
APK：`build/samples/android/spatialxr-xri-palm-lowered.apk`，SHA256 `85b16ce326622e56ade666fe96ddd2aeb17bd3036c8fb6de090481b098f03550`，v2 签名验证通过。
已安装至 `10.168.2.104:5555`，日志确认 `aim=HeadToPalm rayOrigin=Palm pitchOffset=-15 maxElevation=0`，UVC 手动曝光 16.6ms 回读匹配并已采集。
运行日志：`build/logs/xri-stage4-palm-lowered-device.log`。镜片中的角度及目标操作仍需用户复测。

2026-10-12 视频复测与场景参照调整：用户视频显示下压后的射线明显向下，镜片中仍像朝眼睛。
按用户新要求，UVC 示例使用 `HeadForward`，从掌部平行朝头部视线前方发射，上扬 3°，最大仰角 5°；方向不再由掌部相对眼睛的位置决定。
捏合保持、失效取消和固定尺寸移动标记保留。`HeadForward` 追加到枚举末尾，已有序列化方向值保持兼容。
两个方块移至独立 `WorldTargetsRoot`，以场景启动时相机的位置/朝向放置，随后固定在场景坐标；镜头旋转时目标在画面里移动，目标坐标及朝向不变。
调试/UI、估算手骨架和射线输入仍在相机局部坐标中；这提供 3DOF 场景参照，不等同于真实空间位置跟踪。
独立目标根节点在停用时隐藏、恢复时保持原坐标、销毁时清理。验证覆盖双手平行 3°方向、掌部起点、捏合选择释放、相机转动/移动下方块坐标固定及画面位置改变、停用/恢复/销毁。
验证日志：`build/validation/xr-hands-stage4-9c9849f385a840a09934213aa61072e3/verification.log`；镜片中的最终方向感及真实目标选择仍待人工复测。
本轮通过 46 项契约、120 项集成检查、20 次生命周期循环、4 次模拟 UI 点击。
APK：`build/samples/android/spatialxr-xri-world-targets.apk`，SHA256 `c797d00cae290a609c139e6ecf89ed215778e5faf06ef70a21142446c7c5d952`，v2 签名验证通过。
已安装至 `10.168.2.104:5555`，日志确认 `aim=HeadForward rayOrigin=Palm pitchOffset=3 maxElevation=5 targets=worldFixed`；UVC 已采集，手动曝光 16.6ms 回读匹配。
运行日志：`build/logs/xri-stage4-world-targets-device.log`。请人工验证左右转头时方块停留在场景原位置，以及掌部射线方向、白球远去感和捏合选择。

实机待验证：

1. 左/右手分别张开、瞄准两个对象，再捏合/松开；选择和释放各发生一次。
2. 左/右手分别点击 UI，保持捏合不会累加，松开只增加一次；双手同时操作互不串手。
3. 捏合时移出手、关闭识别或拔 USB；没有残留选择，也没有自动 UI 点击。恢复后仍捏合不应直接选中，张开后可继续。
4. 快速捏合、阈值边界抖动及划过目标，记录误触/漏触、瞄准稳定性与恢复时延，按日志决定是否调整阈值。
5. 场景往返、前后台及长时间运行；继续人工确认已有 UVC 权限流程，权限改进另行处理。

阶段 5 的发布制品、新工程完整交付、兼容矩阵仍未完成。标定与光学验收继续保留，近距离抓取未据此开放。

### 2026-10-12：3DOF 食指 UI 与标准手势

当前默认交互取消可见射线、白球、骨架及可选方块，使用食指圆圈操作普通 Button。
应用手部接口为 XRHandSubsystem / XRHandTrackingEvents；手势由原样导入的官方 Gestures 样例 StaticHandGesture 配合 XRHandShape 资产提供，不增加 Spatial 手势事件接口。
验证日志：`build/validation/xr-hands-stage4-30c0de56a02e45a68e626be70ff8836f/verification.log`。
通过 46 项契约、142 项集成检查、20 次生命周期循环、7 次模拟 UI 点击；覆盖无可见线条、食指投影及控制器父节点旋转后圆圈位置、估算手指超过 UI 深度仍能命中、双手独立点击、丢失取消、恢复武装，以及官方握拳/张开/捏合事件和跟踪丢失反馈。
以上为模拟输入自动验证，真实手势阈值、镜片圆圈位置与点击舒适度仍待佩戴验收。UVC-PERM-001 与标定继续搁置。
完整接入与人工清单见 [阶段 4 文档](UNITY_XR_HANDS_STAGE4.md)。
最终 APK：`build/samples/android/spatialxr-xri-fingertip-ui.apk`；SHA256 `cfdccf76ffc3e5840b9af3c568e194283b265111cad5bc8ae94343358dae78a5`，v2 签名校验通过，已安装到 10.168.2.104:5555。
启动日志：`build/logs/xri-fingertip-ui-device.log`；截图：`build/logs/xri-fingertip-ui-device.png`。
标准手势样例的 7 个 C# / asmdef 文件已与 Unity 包内原始文件逐一校验一致。
UI 延迟到初始 XR 姿态更新后摆放，再保持世界坐标固定，避免在场景 Start 时沿尚未更新的相机方向摆放。

### 2026-10-12：保留骨架与方块，圆圈不受捏合弯曲影响

恢复标准 XR Hands 骨架和两个 XRSimpleInteractable 方块，保留世界固定位置与普通 UI。圆圈使用食指根部加掌部横轴 15mm 偏移，与骨架采用相同显示倍率和反射补偿；不读取弯曲指尖来定位，不在捏合时冻结位置，整手移动实时更新。
调试文字顶边下移至约 0.95 单位，字体 30，添加深色背景；OPEN/FIST/PINCH/NONE 合并在左右手文字状态中，不显示按钮式手势块。
应用事件继续使用 Unity XR Hands 官方 StaticHandGesture / XRHandTrackingEvents，方块使用 XRSimpleInteractable，按钮使用 Button.onClick；不新增应用手势接口。
全新 Unity 工程验证日志：`build/validation/xr-hands-stage4-2e8da17e8f9d443e84fb14bd033b2287/verification.log`。检查覆盖圈选方块、松手释放、捏合前后位置不变、捏合期间整手移动、骨架恢复、手势文字与无按钮式图形、下移背景及字体大小、双手 UI 和丢失恢复。
APK：`build/samples/android/spatialxr-xri-hand-circle-cubes.apk`；SHA256 `40f229edd0416f949dc71cfb2a0fd7a05b2b44dd31dc72adc6c96242cceefa7d`，v2 签名通过，已安装至 10.168.2.104:5555。
实机日志及截图：`build/logs/xri-hand-circle-cubes-device.log`、`build/logs/xri-hand-circle-cubes-device.png`。启动确认骨架/方块开启、visibleRays=false、cursorPinchHold=false，UVC 约 30 FPS，GPU VIDEO 识别正常启动，无运行时异常。截图确认骨架、圆圈、两方块、下移文字 UI，且方块悬停正常；镜片合相、圆圈相对食指位置、捏合稳定性和手势准确性仍需人工验收。

### 2026-10-12：按视频调整圆圈位置、目标跟随视线、中央调试面板

依据 `C:/Users/Administrator/456.mp4`，圆圈改用拇指尖与食指尖中点的视线投影，固定深度 0.72 × 展示倍率（默认 2.16 单位）；仅改位置，原有圆环网格、尺寸、材质与颜色逻辑保留。保留骨架和标准 XRI 交互，不新增应用手势接口。
方块与普通 UI 改为 XR 相机局部坐标，随头部转动保持视野位置。调试面板位于方块下方、按钮上方，顶边高 -0.12、底边 -0.46、深度 2.8；深色背景和文字比方块近表面 3.975 更近，避免方块遮挡，不拦截输入。
验证日志：`build/validation/xr-hands-stage4-2e8da17e8f9d443e84fb14bd033b2287/verification-thumb-index-follow.log`。通过 46 项契约、171 项集成检查、20 次生命周期循环、7 次模拟 UI 点击；覆盖圆环尺寸保留、中点镜面投影与深度变化、方块/按钮随相机转动、调试背景前后关系、捏合和丢失恢复。
APK：`build/samples/android/spatialxr-xri-thumb-index-follow.apk`；SHA256 `a105c32e41651669e53aa21960c199bcb82516c95dcf5d11147407bef7ce3709`，v2 签名通过，已安装至 10.168.2.104:5555 并切换到 xri 场景。
实机日志与截图：`build/logs/xri-thumb-index-follow-device.log`、`build/logs/xri-thumb-index-follow-device.png`。日志确认 thumbIndexCursor、headLocked、centralBackground，UVC 约 30 FPS、手动曝光 16.6ms、GPU VIDEO 推理启动。截图确认调试面板在中部偏下，与方块和按钮错开；当前真实输入 hands=0，圆圈相对手部位置、捏合体验及镜片可读性仍待佩戴人工验收。


### 2026-10-12：用户确认阶段 4 最新版本验收通过

用户确认 CPU 默认版本的侧掌补偿、左右手/双手、捏合点击、丢失取消和恢复均无问题，允许进入第五阶段。
本次确认覆盖基础真实交互与最新 SDK 补偿迁移，不替代光学合相、精确标定、长时间性能或免确认 USB 恢复的验收。

# Spatial AI XR Interaction

Unity 2022.3 可选 UPM 包，提供适合 3DOF 的食指圆圈、捏合 UI 示例和 Unity XR Hands 官方手势事件示例。
固定 XR Hands 1.4.3、XRI 2.5.4、Input System 1.7.0、uGUI 1.0.0；TextMeshPro 3.0.6 是官方 Gestures 示例的依赖。

## 应用使用的标准接口

| 用途 | Unity 接口 |
| --- | --- |
| XR 显示与头部姿态 | Unity XR / Cardboard XR Provider |
| 关节与跟踪状态 | `XRHandSubsystem`、`XRHand.GetJoint`、`XRHandTrackingEvents` |
| 手势定义 | `XRHandShape`、`XRHandPose` |
| 手势开始/结束 | 官方 Gestures 示例的 `StaticHandGesture.gesturePerformed` / `gestureEnded` |
| 按钮交互 | `Button.onClick`、`XRUIInputModule`、`TrackedDeviceGraphicRaycaster` |

不新增 Spatial 手势事件、手势枚举或应用输入协议。SDK 的设备适配及去抖向标准 Input System / XRI 组件提供输入；应用无需调用 MediaPipe、JNI 或适配器状态。

## 创建示例

1. 导入基础 SDK、XR Hands 适配包及本包，启用 Input System 或 Both。Android 双目显示配置 Cardboard XR。
2. 在 Package Manager 的 XR Hands 包中导入 **Gestures** 官方样例，或选择 **Spatial AI > XR Interaction > Import Unity XR Hands Gestures**。官方源文件原样导入，等待编译完成。
3. 选择 **Create UVC Interaction Scene**；编辑器测试选择 **Create Simulation Interaction Scene**。
4. 菜单生成动作资产、交互 prefab、`XRHandsStandardGestures.prefab` 和 `OPEN/FIST/PINCH` 标准形状资产。已有资产保留，可在 Inspector 调整。
5. 在标准手势 prefab 中直接绑定 `StaticHandGesture` 的 **Gesture Performed / Gesture Ended** 到自己的应用操作。取消操作另绑定标准 `XRHandTrackingEvents.trackingLost`。
6. UI 使用普通 `Button` 的 **On Click**。世界空间 Canvas 添加 `TrackedDeviceGraphicRaycaster`，场景使用一个 `EventSystem` 和 `XRUIInputModule`。

`StaticHandGesture` 是 Unity 官方样例组件。OPEN/FIST/PINCH 是可编辑的 `XRHandShape` 资产；Unity 没有固定的语义手势枚举，其他手势也用形状/姿势资产定义。
保持形状至少 0.2 秒执行一次；保持期间不重复触发。官方 1.4.3 样例在丢失跟踪时不保证发送 `gestureEnded`，持续操作应同时处理 `trackingLost`。示例已通过此标准事件清除文字状态。

## 交互行为

保留模拟手骨架、两个方块和普通 UI，仅取消可见射线与白球。圆圈位置参照 `456.mp4`，使用拇指尖与食指尖的中点，沿视线投影到固定显示平面（默认显示倍率 3 时距离为 2.16 单位）；不在捏合时冻结。圆圈的网格、尺寸、材质及颜色逻辑不变。
方块和按钮位于相机前方 4.2 单位处，跟随 XR 相机的位置与朝向。圆圈闲置时按左右手显示颜色，悬停变白，捏合变绿；颜色反馈不改变位置。

先张开手，将圆圈移到按钮上，捏合并松开，`Button.onClick` 增加一次计数。丢失、缺失关节或过期数据取消按下，不合成点击；恢复后先张开手。双手独立操作。
将圆圈对准方块，悬停变亮，捏合通过标准 `XRSimpleInteractable.selectEntered` 选中，松开释放；跟踪丢失取消选择。内部 `XRRayInteractor` 执行标准对象/UI 命中检测，线条始终关闭。
调试 UI 放在视野中部、方块下方，带深色背景；其显示深度 2.8 小于方块近表面的 3.975，避免被方块遮挡。按钮行下移，与文字面板错开。手势状态直接显示在左右手文字中（OPEN/FIST/PINCH/NONE），不绘制按钮式手势行。
UVC 单目位置与旋转仍是估算；无标定参数只提示，保留加载流程。首次及 USB 重插权限问题 UVC-PERM-001 暂缓。

默认 CPU 推理。广播 `--es scene xri --es inferenceDelegate GPU` 可调试切换到 GPU，CPU 同理；不带后端参数保留当前配置。调试面板显示 AI/UVC/Render FPS。SDK 已统一侧掌和短暂漏检显示补偿，失效时继续取消交互，应用无需自行实现；详见 [SDK 补偿文档](../../docs/UVC_HAND_SDK_COMPENSATION.md)。
日志关键字 `SpatialXRI`。验证与限制见 [阶段 4 文档](../../docs/UNITY_XR_HANDS_STAGE4.md)。

## 0.1.0-preview.3 交付接入

从同一交付目录安装基础 SDK 0.2.0-preview.11 和本包；Android 显示需配套 Spatial Cardboard。安装流程见基础 SDK Documentation~/INSTALL.md 或交付目录 INSTALL.md。
导入 Hands Interaction 之前先通过菜单导入 Unity XR Hands 官方 Gestures 并等待编译；发布样例包含手势 prefab、形状和 inputactions，保持标准事件接口。

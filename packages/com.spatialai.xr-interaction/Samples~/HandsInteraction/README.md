# Hands Interaction

3DOF 示例用食指圆圈操作普通 Unity UI，捏合松开触发 `Button.onClick`。双手独立，丢失时取消，恢复后先张开手。
保留骨架和两个标准 XRSimpleInteractable 方块，不显示射线或白球。方块/UI 位于前方约 4.2 单位并跟随 XR 相机。
圆圈参照 `456.mp4` 放在拇指尖与食指尖中点的视线投影位置，默认距离 2.16 单位，不在捏合时锁定；原有圆环形状、尺寸和颜色逻辑保留。对准方块可捏合选择并松开释放。
调试文字移到视野中部、方块下方并添加深色背景，显示深度 2.8，比方块更近，避免被方块遮挡。按钮行移到文字下方；OPEN/FIST/PINCH/NONE 在左右手状态文字中显示，不使用按钮式手势图块。

配置 Cardboard Android XR，启用 Input System 或 Both，沿用基础 SDK 的 Android 依赖准备步骤。
导入 Unity XR Hands 的 **Gestures** 官方样例，等待编译，再选择 **Spatial AI > XR Interaction > Create UVC Interaction Scene** 生成完整场景；编辑器使用 **Create Simulation Interaction Scene**。
仅导入本 Samples 场景不会自动导入 Unity 的 Gestures 源码，需完成上述菜单步骤。

定义手势使用 `XRHandShape` / `XRHandPose`，事件使用官方 `StaticHandGesture.gesturePerformed` / `gestureEnded`。
在生成的 `XRHandsStandardGestures.prefab` 中直接绑定应用操作，取消另绑定标准 `XRHandTrackingEvents.trackingLost`。
OPEN/FIST/PINCH 是标准形状资产，不是 SDK 专有手势协议。持续匹配 0.2 秒触发一次。

完整步骤见 [包 README](../../README.md)，验收记录见 [阶段 4 文档](../../../../docs/UNITY_XR_HANDS_STAGE4.md)。

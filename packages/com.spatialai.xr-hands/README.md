# Spatial AI XR Hands（模拟输入与真实 UVC 输入）

这是可选 UPM 配套包，包含标准 `XRHandSubsystem` Provider、确定性的模拟三维手和可视化示例。
运行时登记 descriptor，不自动启动实例；示例启用 `SpatialHandsSimulator` 后创建并启动模拟子系统。
模拟 descriptor 接受 `Simulation` 帧；UVC descriptor 接受经空间转换的 `MonocularEstimate` 帧，不能直接发布二维图像关键点。
基础 `com.spatialai.sdk` 不依赖此包或 XR Hands，现有 `SpatialHandTracking` API 保持不变。

本仓库本地导入时，工程 `Packages/manifest.json` 必须同时指定两个包的 `file:` 路径：

```json
{
  "dependencies": {
    "com.spatialai.sdk": "file:D:/visor-x/spatial-ai-sdk/packages/com.spatialai.sdk",
    "com.spatialai.xr-hands": "file:D:/visor-x/spatial-ai-sdk/packages/com.spatialai.xr-hands"
  }
}
```

只有配套包的路径不能从 Unity 公共 registry 获取未发布的基础 SDK。
XR Hands 1.4.3、Input System 1.7.0、XR Management 4.4.0 是本轮固定基线；
XRI 留到阶段 4。依赖图与编译验证见
[阶段 0 契约文档](../../docs/UNITY_XR_HANDS_STAGE0.md)。
目前 `tools/Pack.ps1` 仍只打包基础 SDK；配套包正式打包与发布属于阶段 5。

## 运行模拟场景

1. 导入两个本地包后，选择菜单 **Spatial AI → XR Hands → Create Simulation Scene**。
   菜单创建并保存 `Assets/SpatialHandsSimulation.unity`；同名文件存在时打开该文件。
   也可从 Package Manager 导入 **Hands Simulation** sample，打开其场景。
2. 点 Play，Game 窗口出现左手青色、右手橙色的关节与骨架。
3. 点击 Game 窗口获得键盘焦点：`1` 张掌、`2` 握拳、`3` 捏合；`L` / `R` 分别切换跟踪，空格切换运动。
4. Inspector 的 `SpatialHandsSimulator` 可分别修改左右手位置、Euler 角与手型。
   `Missing Joint` 或 `Missing Rotation` 选一个关节即可验证部分数据丢失；选 `Count` 表示不注入丢失。

可视化组件从标准 `XRHandSubsystem.leftHand/rightHand.GetJoint` 读取数据，
不直接读取模拟源。它将关节放在 `trackingOrigin` 下；坐标不当作关节局部骨骼坐标使用。
键盘兼容当前启用的 Input System 或 Legacy Input Manager，Inspector 控制不依赖键盘。

模拟组件持有并释放自己的子系统，不接管其他 XR Provider，不更换现有 Cardboard Loader。
动态阶段发布一次帧，渲染阶段复用该快照；有 XR Display 时使用 `Application.onBeforeRender`，
无 XR Display 的编辑器/桌面模拟使用 `LateUpdate` 补足渲染阶段。
停止前清除标准跟踪状态，禁用/销毁组件时退订回调、停止并销毁子系统。

更多验证步骤、证据与未完成能力见 [阶段 1 文档](../../docs/UNITY_XR_HANDS_STAGE1.md)。

## 真实 UVC 场景

安装并配置 Cardboard 后选择 **Spatial AI → XR Hands → Create UVC Scene**，将场景加入 Android Build Settings。
既有 SpatialXR 示例构建会自动加入场景；广播用 `--es scene xrhands`，旧预览仍用 `hands`。
标准场景通过 XR Hands API 读取关节，默认头部局部开发预览。完整使用、坐标与失效行为见
[阶段 3 文档](../../docs/UNITY_XR_HANDS_STAGE3.md)。
缺少标定只提示；严格几何退化仍取消标准跟踪。SDK 可视化组件统一提供侧掌距离保留、120ms 漏检保留和 150ms 无效几何保留，不冒充有效关节；旧预览已共用此流程。
默认 CPU / VIDEO。可通过 `SpatialHandTracking.InferenceBackend` 配置 GPU；Demo 广播增加 `--es inferenceDelegate GPU/CPU`，省略参数保持本进程设置，新进程默认 CPU。
调试文字显示 AI、UVC、Render FPS；配置、补偿和验证步骤见 [SDK 补偿文档](../../docs/UVC_HAND_SDK_COMPENSATION.md)。

## 0.1.0-preview.3 交付接入

从同一交付目录安装基础 SDK 0.2.0-preview.11 和本包；Android 显示需配套 Spatial Cardboard。安装流程见基础 SDK Documentation~/INSTALL.md 或交付目录 INSTALL.md。

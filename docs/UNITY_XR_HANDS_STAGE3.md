# Unity XR Hands 阶段 3：真实 UVC 输入、时序与恢复

日期：2026-10-10。软件实现和编辑器验证已完成，完整实机验收另列待执行项。

## 交付

- `UvcHandsFrameSource`：主线程把 Android 图像关节转换为不可变空间快照，保留估算来源和原始手别；重复输入不重复发布。
- `SpatialHandSubsystem`：保留模拟 descriptor，新增 `SpatialAI-UVC-Hands`，通过标准 XR Hands 根姿态、26 关节和跟踪事件发布严格有效数据。
- `SpatialUvcHands`：管理唯一 Spatial 子系统、订阅或创建相机组件，Dynamic 与 BeforeRender 复用同一空间帧。
- 相机原始轮询组件增加全局相机所有权和失效事件，关闭识别、禁用、暂停后通知消费者。
- `SpatialUvcHandsSample` 和 `Spatial AI > XR Hands > Create UVC Scene`：独立标准接口消费场景；不依赖旧 HandTrackingDemo 来读取关节。
- 示例广播增加 `scene=xrhands`，保留原有 `hands`、`imu`、`optics`、`calibration`。
- `tools/ValidateXRHandsStage3.ps1`：独立工程的契约与 Play 模式生命周期检查。

不重复打开相机：Driver 优先订阅已有启用的 `SpatialHandTracking`，没有才创建自己的组件。
停止 Driver 时仅停用自建相机；借用相机继续由原持有者管理。启停重用自建组件，不因延迟 Destroy 产生重复实例。
Android 默认 CPU / VIDEO；显式 GPU 配置、模型创建、推理与关闭都在 CameraService 消费线程执行。GPU 初始化失败回退 CPU，分类失败回退 CPU 关键点模型。

## 数据和显示边界

标准接口输出米制、未经显示反射/放大、tracking origin 坐标的关节。能力仅声明估算位置和旋转，不伪造半径与速度。
Unity XR Hands 本身没有“单目估算”专用跟踪位，应用可通过 `SpatialHandSubsystem.LastFrame` 查询 Source 和 Estimated 字段。

严格几何失败时该侧 Untracked，另一侧可继续跟踪；`depth-held`、`missing-held` 或 150ms 保留不作为有效空间数据。
2026-10-12 已将身份稳定和显示补偿迁入基础 SDK，旧 `hands`、标准 `xrhands` 与 `xri` 可视化共用处理器，
标准关节、跟踪事件与交互仍按实际有效数据更新。详细配置和显示边界见 [SDK 补偿文档](UVC_HAND_SDK_COMPENSATION.md)。
标准示例展示层保持 3 倍距离、镜片 X 补偿；左手青色、右手橙色，与模拟 XR Hands 可视化一致。

默认实机示例明确是**头部局部开发预览**：缺少真实物理头部来源时转换使用 Pose.identity，消费者显示在相机下。
这不等于固定世界坐标跟踪，也不能把补偿后的渲染相机填入 physicalHead。
要在固定 XR tracking origin 中发布关节，启动前给 Driver 指定 `trackingOrigin`、独立 `physicalHead` 变换，
或由自己的物理头姿系统维护该变换；归零/换 origin 时增加 `originRevision`。合成测试验证了平移与旋转后的原点输出。
物理头姿、安装外参和光学验收仍待实机测量，不能仅凭骨架显示正常宣称通过。

缺少标定参数继续提示并使用开发默认值，加载 TextAsset JSON 的入口保留。
存在错误 JSON、无效头姿或与相机尺寸不匹配时拒绝空间数据，不隐式吞掉配置错误。

## 时序与恢复

- 相机结果在 Unity 主线程接收并转换；Dynamic 和 BeforeRender 可以多次读取同一帧，不制造新推理序列。
- Camera 的尺寸/身份变化、源序列回退、origin revision 变化重新建立转换会话与身份，避免旧标签继续复用。
- 无手结果立即取消标准跟踪；识别关闭、相机停止/暂停/禁用和 USB disconnect/closed 状态立即清空。
- 无新结果超过 350ms 时失效；帧到达时已经积累的 Android 结果年龄也计入超时，恢复的新帧可重新取得跟踪。
- 前后台暂停停止并销毁子系统，回来后重新创建；禁用/销毁会先通知标准消费者失去跟踪。

Android `receivedAtNs` 是识别完成时间，不是曝光时间。它与 Android SystemClock 的差只用于计算同域的结果年龄；
Unity 接收时间用 HostStopwatch，跨时钟只传递已计算的持续时间，不相减绝对时间戳。
曝光来源时间保持 Unknown，不能将主机结果年龄或 captureToResultMs=-1 描述为传感器延迟。

## 使用

在基础 SDK 和 Cardboard 包之外导入本地 `packages/com.spatialai.xr-hands`；其依赖固定 XR Hands 1.4.3。
既有 SpatialXR 工程构建脚本检测到适配包后自动生成并加入 `SpatialUvcXRHands` 场景。
全新项目先配置 Cardboard Android XR，再通过菜单创建 UVC 场景并加入 Build Settings。
导入时未安装 Cardboard 则只提示，仍可运行编辑器合成验证；手机立体显示需要实际 XR loader。

```powershell
adb -s 10.168.2.104:5555 shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene xrhands --ez gestures true --ei exposureMode 1 --ei exposureTimeUs 16600
```

切回改善过的骨架预览使用 `--es scene hands`。不需要删除或改动旧场景。

日志关键词 `SpatialXRHands`，实际 logcat 标签仍为 Unity：

```powershell
adb -s 10.168.2.104:5555 logcat -v threadtime Unity:I SpatialHands:I SpatialUVC:I AndroidRuntime:E '*:S' > xr-hands-stage3.log
```

每 5 秒统计采集 FPS、发布 FPS、渲染 FPS、结果年龄/推理/转换耗时 mean/P50/P95、跳过的采集序列、模型模式和 delegate。
跳过采集序列含推理限速和只读取最新结果造成的间隙，不等于已经推理完成但丢失的帧。
耗时分布每个窗口最多保留 240 个样本；年龄按 Dynamic 更新采样。GPU 回退以实际 delegate/gestureSource 为准。

## 验证状态

`build/validation/xr-hands-stage3-ea690cb6d0ad4662902e4174dd00733d/verification.log`：96 项契约检查通过；Play 模式 20 次启停、暂停恢复、
重复实例拒绝、识别开关、Dynamic/BeforeRender 和推理帧不重复计数通过。此处输入均为合成数据，不是实机精度证明。
验证从全新隔离工程导入基础 SDK 与 XR Hands 包开始，包含场景生成及 Input System 头姿组件。
显示标记和 UI 背景使用纯网格，无需碰撞组件，避免 Android IL2CPP 裁剪隐式组件产生运行错误。

阶段 1 回归：`build/validation/xr-hands-stage1-9b7d9ff56f184f3d8f06ed4544caf1aa/verification.log`，
307 项契约检查与两次 Play 会话通过，包含标准可视化与生命周期检查。

最终 APK：`build/samples/android/spatialxr-xr-hands-stage3.apk`，54,611,483 字节，v2 签名校验通过。
SHA-256：`44786035b6f56d79f53f069e25288abde484afb32b4eb63880feb79e5e2ab016`。
构建日志：`build/logs/xr-hands-stage3-final-apk.log`。已安装到 Xiaomi 2206123SC / mayfly（10.168.2.104:5555）。

启动记录使用 640×480@30、手动曝光 16.6ms、VIDEO/GPU；无手时采集约 30FPS，
启动后识别/发布约 19FPS；恢复后稳定窗口识别约 23.5～24.7FPS、标准发布约 20.6～21.8FPS，
Unity 渲染约 60FPS。推理完成率与 Unity 读取最新结果的发布率分别统计，不能混为同一个 FPS。
此次无手基线不是此前有手 22～24FPS 的重复验收，
也不证明阶段 3 提升了模型性能。结果年龄是识别完成后的年龄，不是曝光到显示的延迟。

实机冒烟通过：关闭/恢复识别、`imu -> xrhands -> hands -> xrhands`、Home 后回到应用，
每次重新创建的 UVC Provider 和 VIDEO/GPU 模型正常启动。最终场景为 `SpatialUvcXRHands`。
运行日志 `build/logs/xr-hands-stage3-final-device.log` 未发现 Unity 错误或致命异常。
检查期间未检测到手，因此上述记录证明启动与生命周期链路，单/双手有效姿态和跟踪丢失事件仍需佩戴复测。

## 用户实机复测（2026-10-10）

用户确认以下功能验证通过：

- 左右手骨架、移动方向和双手同时显示正常。
- 手移出后消失，返回后恢复跟踪。
- USB 拔出再插入后，重新获取并人工确认权限，能够恢复识别。

USB 恢复通过的前提是用户再次确认权限，不代表已实现免确认恢复。
本次反馈未提供连续 60 秒采样、精度或长时间资源数据，相关验收仍保留待执行。

## 待处理：UVC 授权流程（UVC-PERM-001）

状态：已记录，按用户要求暂缓解决，本次不修改授权代码。

现象：首次打开应用需要用户确认权限；USB 摄像头拔插后也需要重新获取并确认权限。
影响：首次使用和设备重连需要人工操作，尚不能直接恢复识别。

后续工作：

1. 记录首次启动、USB 重插、进程重启与前后台切换的授权日志，区分 USB 设备授权和 Android 运行时权限。
2. 检查 Android 相机服务的权限检测、请求、授权结果回调与设备重连流程，确定再次弹窗的原因。
3. 评估授权复用和减少重复请求的可行性；需要用户确认时，提供明确状态、确认后的自动启动及拒绝后的重试入口。
4. 回归首次使用、已授权重启、USB 重插、拒绝后重试，确认没有重复弹窗、重复打开相机或卡住恢复流程。

问题同时覆盖首次启动与 USB 重连；尚未确认根因，不将其归因于某一具体权限或固件。

## 剩余实机验收

1. 固定曝光和光照，预热后无手、单手静止/移动、双手各记录至少 60 秒。
2. 左右手显示和移出/返回已由用户确认；补充标准跟踪事件与关闭识别的时延记录。
3. USB 重插后人工授权恢复已由用户确认；补充资源/重复实例检查，授权流程改进见 UVC-PERM-001。
4. 重复 `imu -> xrhands -> hands -> xrhands`，切后台再回来；确认没有 GPU 异常或残留骨架。
5. 长时间运行记录温升、性能和资源趋势；真实 GPU 初始化失败与分类失败路径需异常注入或自然发生的日志，合成检查不能代替。

后续 [阶段 4](UNITY_XR_HANDS_STAGE4.md) 已实现 XRI 远距离射线、捏合选择和 UI 输入，实机交互待验收；近距离抓取仍未开放。

# UVC 翻手、手别跳变与三维骨架日志

日期：2026-10-10。当前小米/HXQ 已启用显示 X 补偿和解剖左右手交换。
用户反馈翻到侧掌时骨架消失、同一只右手偶尔被分类为左手。本轮提供稳定处理及诊断，
依据首轮实机日志增加侧掌显示退化与短暂漏检保留，不以某个手势名称或分数作为固定触发条件。

2026-10-12 更新：身份关联、侧掌距离保留和短暂漏检显示已由 SDK `SpatialUvcHandProcessor` 统一处理；
旧 `hands` 场景只消费处理结果，不再自行维护补偿策略。标准 XR Hands 场景的 SDK 可视化也使用相同缓存。
默认 CPU、GPU 调试切换、三项 FPS 与日志边界见 [SDK 补偿文档](UVC_HAND_SDK_COMPENSATION.md)。

## 当前处理

- 在 UVC 图像中按手腕位置关联最多两只手，匹配距离门限为归一化 0.18；双手按整体最小距离分配，
  不依赖输出数组顺序。连续轨迹保留首次取得的手别，原始分类仍保留在每帧数据中。
- 连续 250 ms 未检测到该手、较大位置跳变、相机/尺寸变化或帧号重启后重新建立身份。
  漏检时画面最多保留 120 ms；250 ms 是身份关联保留时间，不是骨架显示时间。
- 几何转换拒绝当前帧但同一身份仍被检测到时，显示层最多保留最近有效骨架 150 ms。
  UI 标为 `held`，期间不能触发目标选择。SDK 转换结果仍是 Untracked，不伪造新有效空间样本。
  超时、不同身份或参数错误后不保留。正常空间转换时显示 `fresh`。
- 掌宽/掌长深度冲突时，同一身份已取得有效距离且 21 点都可投影，使用当前关键点和最近有效的相机距离显示，
  UI 标为 `depth-held`。骨架会跟随翻转与移动，但前后距离暂不更新；先以正常掌姿取得距离，再翻手。
  同一连续身份可持续退化展示，恢复有效几何时重新估距。不同身份不能继承旧距离，其他拒绝原因不进入此路径。
- 当前模型没有检测到该手时，只对尚存连续身份保留最近展示骨架最多 120 ms，标为 `missing-held`。
  超时按渲染帧隐藏，不依赖下一张识别结果；相机/尺寸/序列重置和身份失效时不保留。
  `depth-held`、`missing-held` 和 `held` 都只用于展示，不能悬停或选择目标。
- 单目几何检查继续保留：掌宽/掌长推算冲突、距离越界、掌面退化仍拒绝。
  退化展示减少消失，不解决侧掌造成的深度不可观测，SDK 仍返回 Untracked。
- 首次手别错误及双手重叠/快速替换仍可能误关联；先以清楚的正常掌姿进入视野。
  需要重新取得身份时，将手完全移出视野超过 250 ms 后再伸入。

## 分数与字段

UI 的 `G` 是手势分类分数，`rawH` 是模型原始左右手分类置信度。旧 AAR 没有 rawH 时显示 `--`，不能拿 G 替代。
原始手别与稳定身份不一致时标记 `LOCK`；rawH 仍属于原始分类，不代表稳定标签的置信度。
Android 桥接增加 `handednessScore` / `hasHandednessScore`；原始 `rawHandedness` 和 `rawGesture` 保留。
Unity 另存 `stabilizedHandedness` / `trackingId`，不改写原始识别帧。
默认小米的 `uvcSwapHandedness=true` 在关联后统一映射解剖手别；已加载 JSON 时以 `swapHandedness` 为准。

日志关键字 **`SpatialHandsDiag`**。Unity 的 Android logcat tag 是 **`Unity`**，不是 SpatialHandsDiag。

| 日志内容 | 含义 |
| --- | --- |
| `acquired` / `expired` / `reset` | 取得身份、超时、相机/尺寸/序列/时钟重置 |
| `label_change` | 原始分类发生变化；同时显示 track、raw 和 stable |
| `Left=held` / `Right=held` | 当前空间转换失败，暂用最近有效骨架 |
| `depth-held` | 当前关键点投影，沿用该身份最近有效的相机距离 |
| `missing-held` / `missing_hold_expired` | 短暂漏检保留及 120 ms 保留到期 |
| `leftCameraDepth` / `rightCameraDepth` | 当前展示采用的相机距离，单位米 |
| `Left=hidden` / `Right=hidden` | 保留超时、手离开、无有效几何等 |
| `Left=fresh` / `Right=fresh` | 新有效几何或恢复 |
| `stale` | 超过 350 ms 没有新识别结果 |
| `leftReason` / `rightReason` | 实际转换结果；包含 widthDepth、lengthDepth、ratio 或 depth/min/max 等数值 |
| `rawSide` / `stableSide` / `mappedSide` | 模型原始手别、连续轨迹手别、应用中解剖手别 |
| `handednessScore` / `gesture` / `gestureScore` | 当前帧独立的 H 与 G；没有置信度时注明 unavailable |
| `snapshot seq=… track=… part=…` | 问题帧的 21 点与原始标签/分数 JSON，按同一 seq/track 的 part 顺序拼接 |

所有事件带 `seq`，帧头含 camera、尺寸、主机接收时间、FPS 和推理耗时。
身份/显示状态变化时立即记录，正常状态约每 2 秒摘要一次；问题快照最多每 0.5 秒一组。
重复拒绝仍定期记录，可以捕获持续失败时的实际姿态。手势可为任何类别，分数也没有固定匹配条件。
首轮实机快照的 1400 字符分片被 Unity Android 日志截断，现改为 600 字符，留出消息前缀余量。

## 实机抓取

先在电脑开始记录，再在手势场景慢慢翻手，尽量保留问题前后各 5～10 秒。
只伸一只手，重复“正常掌姿 → 侧掌 → 恢复 → 完全离开 → 重新伸入”。
在 PowerShell 执行（电脑可找到 adb 时）：

```powershell
adb -s 10.168.2.104:5555 logcat -v threadtime Unity:I SpatialHands:I SpatialUVC:I AndroidRuntime:E '*:S' > uvc-hand-turn.log
```

复现后按 Ctrl+C 停止，把 `uvc-hand-turn.log` 发来，并说明伸的是哪只手、哪次翻转发生异常。
不要只复制最终一行：需要前后的身份事件、拒绝原因和完整 snapshot part。
如果刚发生异常，也可立即导出当前缓冲：

```powershell
adb -s 10.168.2.104:5555 logcat -d -v threadtime Unity:I SpatialHands:I SpatialUVC:I AndroidRuntime:E '*:S' > uvc-hand-turn.log
```

## 构建来源

本轮 Android AAR SHA-256：`2899ec3ed3d0f7ce2f423a29d43e90263892f43f7b4680ae976089a1ae8a3fcf`。
由 Android 工作区中的 `SpatialHandBridge.java` 修改构建；锁文件以 `sourceState=local-working-tree-hand-confidence-diagnostics`
及修改文件 SHA-256 明确记录，revision 是此前提交的基线，不能宣称该提交能直接重建新制品。
源代码提交后应更新锁定 revision 并去除本地状态。AAR 仍是忽略的生成产物；
`VerifyAndroidHandAar.ps1` 验证当前哈希，并阻止从不完整的提交来源恢复本地制品。

核心回归：`build/logs/uvc-hand-core-regression.log`；阶段 2 回归：`build/logs/uvc-hand-stage2-regression.log`；
稳定/退化/日志集成验证：`build/logs/uvc-hand-stability-check-final.log`。

2026-10-10 验证：核心 976 项、阶段 2 转换 545 项、Unity 示例集成 181 项检查通过。
新版 APK：`build/samples/android/spatialxr-uvc-hand-diagnostics.apk`，53,532,371 字节，v2 签名通过。
SHA-256：`430814256f91ed7e59e28ec2d5518a0f67b334115920b92167e899b2fa1ae296`。
已安装到 `10.168.2.104:5555`；实际翻手稳定程度和持续侧掌的几何拒绝原因待复现日志确认。

实机启动检查：示例进入 HelloSpatialXR，摄像头首次启动后发生 USB 断开；重连时前台为
系统 UsbPermissionActivity，当前等待用户在手机允许 USB 相机访问。尚未获得本轮实机推理帧，
因此新增原始手别分数与问题快照的实机输出待授权后复现确认。启动记录：build/logs/uvc-hand-diagnostics-device.log。

本轮 APK：build/samples/android/spatialxr-uvc-side-palm.apk，53,533,407 字节，v2 签名校验通过。
SHA-256：1eba64ef2e99b1aa5c2c0156ef5d3db86e8c20f43dad5105e4d879a6ecc43b53。
已安装到 10.168.2.104:5555 并启动；构建日志 build/logs/uvc-side-palm-apk.log。
实机启动检查通过：手势场景运行，UVC 640x480 @30，手动曝光 16.6 ms，captureFps 约 30.0，
previewFps 约 29.9；新增 SpatialHandsDiag 相机距离字段已输出。检查时未检测到手，实际翻手与
实机快照完整性仍需用户复测，不能以编辑器检查代替。日志：build/logs/uvc-side-palm-device.log。

### 手势场景远距离显示（2026-10-10）

为方便合相调整，HandTrackingDemo 的 presentationDistanceScale 默认 3，可在 Inspector 启动前设置 1～10。
骨架、目标、指针、调试 UI 和背景统一放在相机下的展示根节点，以等比缩放放远，保持原有角大小和局部交互坐标。
目标从 0.8 m 显示到 2.4 m；UI 从 1.8 m 显示到 5.4 m；骨架显示距离为转换输出的 3 倍。
UI characterSize 从 0.006 改到 0.0075，额外放大 25%，背景同步扩大 25%。
这是显示距离调整，SDK 关节、标定与日志中的物理估计距离保持不变；不能据此宣称完成双眼光学校准。

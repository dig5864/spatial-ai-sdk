# 原生 Cardboard IMU 对照

## diagnostics-v6：120 帧时序诊断

本轮保留 prefer120 / display-time，增加诊断，不调预测系数、融合算法、光学或场景。**v6 诊断包的 SDK 二进制因插桩重编译而不同于 v1–v5**。`PrepareNativeImuDiagnostics.py` 复制当前固定版本及已有光学补丁到 build 下，在副本的 `PredictRotation` 持锁期间读取实际用于本次预测的样本与姿态；共享 SDK checkout 和 Unity AAR 不被替换。构建记录同时保存 baselineNativeSha256、诊断库哈希和插桩文件哈希。

构建命令：

```powershell
python -X utf8 tools/BuildNativeImuComparison.py --diagnostics-sdk --unity-log D:/visor-x/output/imu-r2-layout/startup.log
```

使用上一节同样的 prefer120 / display-time 启动命令；若需重新启动：

```powershell
adb -s 10.168.2.127:43495 shell am start -S -n com.spatialai.samples.cardboardnative/.MainActivity --es refreshPolicy prefer120 --es predictionMode display-time --ei predictionMs 50
```

静置 10 秒，再执行：

```powershell
python -X utf8 tools/CaptureImuDiagnostics.py --seconds 30
```

看到 `Capturing` 后进行慢转、起动、快速换向和停止。计时完成后还会下载系统跟踪文件，Wi-Fi 下可能需要约一分钟；以 `Complete with pipeline trace` 为全部完成标记。应用保持打开。

新增 `native-diagnostics.csv`：SDK 样本 BOOTTIME、进入融合函数的 BOOTTIME、查询时刻、实际预测角速度、融合坐标系预测前后四元数、姿态查询和 CPU 绘制起止。快照通过同线程 TLS 导出，校验版本及目标时间与当前查询一致；没有另开传感器监听器，也没有二次查询取近似值。sampleToFusionMs 包含系统传递及 SDK 轮询，不是硬件驱动单独耗时；predictionDeltaDeg 是外推修正量，不是相对真实头部的误差。逐帧日志缓冲写盘，插桩和系统跟踪自身可能带来开销。

`CaptureImuDiagnostics.py` 同时采集 Perfetto gfx / 应用 trace（SpatialNative.draw、SpatialNative.getPose、平台 eglSwapBuffers 等）。trace 每秒写入文件，避免仅保留缓冲区中一段；不根据 queue 深度直接换算延迟，也不将 eglSwapBuffers 返回视为已显示。若设备仍无 EGL 实际呈现时间，无法从这些记录精确给出运动到光子延迟。

SurfaceFlinger 改为手机端每 250 ms 采样后整体拉取。以首个设备端快照作为排除基线，并用设备 uptime 差计算采样时长；主机上传、下载和等待时间另存 hostElapsedSeconds，不纳入采样时长。保留原始设备脚本与输出流。仍检测历史缺口，不放宽 PASS 门槛。原 `CaptureImuPacing.py` 可用 `--device-poll` 单独启用该方式。

功能验证：13 项 Python 测试通过；诊断包编译、安装成功；`native-diag-v6-final-20261023-170534` 校正设备端基线后 1833 个间隔，覆盖 97.79%，P99 8.240 ms，异常 0%，PASS。此预检基本静止，样本年龄中位数约 6.6 ms、进入融合器前约 5.3 ms；不是用户运动验收。最初 32 MB 非流式 trace 仅保留约 10.5 秒，相关管线统计只适用于该片段；后续已改成持续写盘。

最终持续写盘验证 `native-diag-v6-stream-check-20261023-170828`：1290 个呈现间隔、覆盖 96.96%、P99 8.242 ms、无历史缺口，PASS；1290 个有效 SDK 快照，无负时间、重复样本或换算错误。传感器样本年龄 P50 6.592 ms / P99 8.705 ms，sampleToFusion P50 5.321 ms，CPU 姿态查询 P50 0.0216 ms，CPU 绘制 P50 0.734 ms。Perfetto 保存约 24.93 秒、3027 次 draw/getPose/swap，未再报告数据丢失；eglSwapBuffers 平均 1.379 ms、P99 2.798 ms、最大 4.712 ms。这些调用耗时不包括提交后的全部显示等待。工具提示流式 trace 应设 flush_period_ms 以改善解析内存占用，已补充为 1000 ms。最终测试包及构建记录归档 D:/visor-x/output/native-cardboard-diagnostics-v6/。

## refresh-v5：120 帧实验

新增 `--es refreshPolicy prefer120`：请求同分辨率约 120 Hz 模式，并以实际刷新率相对于 120 FPS 整数分频。实际 120 Hz 时每个回调出帧，144 Hz 时 144 FPS，240 Hz 时 120 FPS；若系统仅提供/允许 60 Hz，则只有 60 FPS，不能将请求值当作实际值。默认仍为 prefer60。预测、光学、场景沿用 v4。

```powershell
adb -s 10.168.2.127:43495 shell am start -S -n com.spatialai.samples.cardboardnative/.MainActivity --es refreshPolicy prefer120 --es predictionMode display-time --ei predictionMs 50
```

等待 10 秒后采集：

```powershell
python -X utf8 tools/CaptureImuPacing.py --package com.spatialai.samples.cardboardnative --label native-120-v5 --seconds 30
```

终端应显示 mode 120 Hz / target 120 FPS。同 APK 对照时把 prefer120 改为 prefer60，保留 display-time。刷新周期改变会使动态预测提前量随系统时间线变化，这是该模式的预期行为。量化工具自动采用应用报告的目标 FPS，120 FPS 单帧预算约 8.33 ms。手机开发者角标是显示刷新率，应用实测出帧以 SurfaceFlinger 相邻呈现间隔为准。

实机预检 `native-120-v5-smoke-20261023-165005`：实际模式 120 Hz、divisor=1，1628 个已知呈现间隔平均 8.238 ms、P99 8.242 ms、最大 8.262 ms，已观测样本异常 0%。采集历史有缺口且覆盖 88.49%，完整验收 INCONCLUSIVE，不能据此断言整轮无丢帧。动态预测提前量中位数 22.881 ms，无回退/过期/时钟换算错误；该数字不是实测运动延迟。刷新调度测试覆盖原策略 8 种与 120 目标 6 种刷新率，分析器 10 项测试通过。APK 归档 D:/visor-x/output/native-cardboard-refresh-v5/，SHA256 cb768d960c3d4a826f0b4ab8204660568b19358d03373a7205f6debeff0da0d1。手机留在 prefer120 / display-time 模式。

独立 Java/NDK/OpenGL ES 3 应用，包名 `com.spatialai.samples.cardboardnative`，桌面名称 **Cardboard IMU Compare**。与 Unity 示例共存。Java 仅负责 Activity、Surface、60 FPS Choreographer 调度和触摸；场景、头部姿态与畸变在 C++ 渲染，不运行 Unity。

## 与当前 Unity 包保持一致的内容

从传入的 Unity 启动日志提取实际应用的双眼矩形和显示基线，而不是套用通用 Cardboard QR 镜片参数。当前日志来自 Xiaomi 2206123SC / mayfly：

- 全屏 2400×1080；左眼 `(268.217,157.0452,865.0568,865.0535)`，右眼 `(1266.727,157.0452,865.0568,865.0535)`，单位为 Android 左上角起算像素。
- 每眼网格输出旋转 180°，保留原纹理 UV 方向及矩形边界三角形裁剪。
- RhinoX Sharp 网格源 SHA256 `9ce72f86ada58cf53ab0d440fd504c1197987918be09770f0852faea5512d969`。
- 原生 Cardboard 库与 Unity B 基线逐字节相同，SHA256 `cf4c49318f366a12ba8f665dc2f1ae3f7fc5f91299f0517f4d9920cfed865dd4`；固定源码提交 `67bee10850562bf8cbe563a22b47eca8ffd8f1eb`。
- 显示基线 -0.062 m，物理 IPD 仍为 0.062 m。左右/上下视锥沿用 `rhinox-pro-reference.json`；每眼纹理 1200×1080，与插件相同。
- 相同 LandscapeLeft、Cardboard→Unity 四元数转换、机型 mount 和反射支架 pitch/roll 补偿。基线采用未来 50 ms 的预测查询；prediction-v3 可通过启动参数单独调整并记录实际值。调用原库的主机映射函数，不另做融合算法。
- refresh-v2 默认在当前分辨率的支持模式中优先请求约 60 Hz；系统决定是否接受。读取实际 Display mode / app-visible refresh rate，监听变化并每秒复核；不修改手机全局设置，不使用 Swappy。
- 应用按实际 app-visible refresh rate 的整数分频出帧，尽量达到至少约 60 FPS：60→60、120→60、144→72、90→90、165→82.5。59.94 / 119.88 等模式容差处理。其他手机具备 60 Hz 模式且请求生效时，仍优先使用约 60 FPS。系统拒绝模式切换时使用实际刷新率，不声称请求值已经生效。
- refresh-v2 用刷新周期的半周期容差调度，不再使用固定 16 ms 门槛。回调遗漏时跳过已错过的时隙，不突发补帧；模式变化时重置调度。这是原生应用的调度实验，不能声称与 Unity 出帧实现相同。
- 相同 4 个方块尺寸、距离、偏移和颜色；4×2 m 棋盘、0.2 m 格子、距离 2.6 m；XYZ 轴位置、长度和颜色。轴杆用细长长方体、文字用矢量笔画代替 LineRenderer / TextMesh，几何测试目标一致，字体光栅化和 draw call 数量不是完全相同。
- 默认无左侧调试面板；触摸屏幕可将测试目标重新摆到正前方，对应 Unity 的 ZERO + face axes。Android 返回键退出。

完整输入、来源日志路径及哈希保存在 APK 的 `assets/comparison-config.json`。当前光学标定沿用现有工程的未完整验收状态；这是同参数的对照，不是重新完成光学标定。

## 构建

Windows 离线构建，依赖已准备好的 Cardboard checkout / AAR、CMake、Ninja、Unity 的 NDK / OpenJDK 和 Android SDK 34。所有输出及调试签名都在忽略的 build 目录；不修改上游或 Unity 工程设置。

```powershell
python -X utf8 tools/BuildNativeImuComparison.py --unity-log D:/visor-x/output/imu-r2-layout/startup.log
```

参数 `--android-player`、`--android-sdk` 可覆盖工具路径。脚本对 SDK 库、网格、日志中的姿态/矩形/基线做校验；参数不匹配时拒绝生成看似相同的对照包。

输出：`build/samples/android/cardboard-imu-comparison/spatial-cardboard-imu-comparison.apk`。

```powershell
adb -s 10.168.2.127:43495 install --no-incremental -r build/samples/android/cardboard-imu-comparison/spatial-cardboard-imu-comparison.apk
adb -s 10.168.2.127:43495 shell am start -W -n com.spatialai.samples.cardboardnative/.MainActivity
```

新包可能触发手机端安装确认；`INSTALL_FAILED_USER_RESTRICTED` 需要在手机端允许安装，构建工具不绕过限制。Unity 包保留在手机上，可通过原 Activity 切回。

## 量化采集

请先打开桌面的 **Cardboard IMU Compare**（包名 `com.spatialai.samples.cardboardnative`）。采集脚本不会启动或退出手机应用；如果打开的是其他应用，会显示实际前台包名并退出。终端每约 5 秒打印进度，计时结束后保存上下文、CSV 和报告，显示 `Capture finished` 后脚本退出。手机仍停留在测试场景是正常行为，可用 Android 返回键退出。`com.ximmerse.xr.cardboardslidein` 是另一个应用，不是本次原生对照包。

关闭设置，等待启动至少 10 秒，保持相同的安装姿态、亮度、温度和运动方式；建议交替 Unity→Native→Unity，并核对每段实际刷新率。不要把不同显示模式或不同运动速度下的差异全归因于引擎。

```powershell
python -X utf8 tools/CaptureImuPacing.py --package com.spatialai.samples.cardboardnative --label native-imu --seconds 30
python -X utf8 tools/AnalyzeImuPacing.py build/validation/imu-pacing/<本次目录>
```

同一采集工具支持 Unity（省略 --package）。原生每 5 秒打印 `SpatialNative WINDOW`；它是渲染线程间隔，不是实际呈现间隔。统一判定以 SurfaceFlinger 的实际呈现时间戳为主。

refresh-v2 每 5 秒额外输出 `DISPLAY_RATE`，启动输出 `DISPLAY_REQUEST`（支持模式、请求模式 ID），改变模式时输出 generation。应用写入 `refresh-state.json`，采集工具自动读取实际目标 FPS 和请求是否生效。采集期间 generation 变化，即使最后回到原来的 Hz，也判为 INCONCLUSIVE，等待稳定后重新采集。正式采集应在启动后等待至少 10 秒。

如需仅跟随手机当前刷新率、不优先请求 60 Hz，可以启动以下独立实验模式（每轮需重新启动，不在采集中途切换）：

```powershell
adb -s 10.168.2.127:43495 shell am force-stop com.spatialai.samples.cardboardnative
adb -s 10.168.2.127:43495 shell am start -n com.spatialai.samples.cardboardnative/.MainActivity --es refreshPolicy follow-display
```

恢复默认：force-stop 后省略 refreshPolicy 参数启动。当前光学配置仍来源于这部 2400×1080 手机；刷新率适配并不表示其他手机的光学/尺寸参数已完成标定。

官方依据：[Android 多刷新率模式](https://source.android.com/docs/core/graphics/multiple-refresh-rate)、[刷新率请求与变化通知](https://developer.android.com/media/optimize/performance/frame-rate)。应用模式请求可能被系统策略覆盖，必须核对实际模式。

支持 `EGL_ANDROID_get_frame_timestamps` 且设备提供 DISPLAY_PRESENT_TIME 时，原生将帧 ID、姿态查询单调时刻、未来预测 BOOTTIME、绘制结束、实际开始扫描时间和四元数写入 app 自己的 `native-frames-*.csv`。缓冲写入，每约 1 秒刷新，不逐帧输出 logcat。首 5 秒标记 warmup。缺失、待定或不支持的显示时间写为 0，不当作 0 延迟。采集工具会拉取最近的 CSV；分析只取与本次 SF 呈现窗口相交的有效行。

`pose query → scanout` 是软件管线时间；缺少物理运动发生时刻、融合内部传感器样本时间、扫描行位置与像素响应，不等于真实运动到光子延迟，也不等于固定 50 ms 预测量。

可选可重复运动模式（默认关闭，日志明确标记 synthetic）：

```powershell
adb -s 10.168.2.127:43495 shell am force-stop com.spatialai.samples.cardboardnative
adb -s 10.168.2.127:43495 shell am start -n com.spatialai.samples.cardboardnative/.MainActivity --ez synthetic true
```

它使用 ±25°、0.25 Hz 正弦视角，便于检查渲染节奏，不用于验收真实 IMU 跟随或与 Unity 的真实运动样本混比。省略该参数重新启动恢复真实 IMU。

## 2026-10-23 实机交付

已在 `10.168.2.127:43495` 的 Xiaomi 2206123SC 上安装和启动。确认输出 2400×1080、每眼纹理 1200×1080、系统实际 120 Hz；截图核对双眼棋盘、方块与 XYZ 标签。已验证同一进程 Home 暂停时释放 tracker/CSV、返回后重新初始化并继续渲染、触摸重置场景锚点。

APK SHA256：`1984139dedd3ce06bc93e330dd69aae1b60a12327232185eec34dce294fb15c7`；配置哈希：`1261c614d8c5b4d36687b10ec72e6c7fc555468907222363b882d6386b085d22`。打包后再次核对 Cardboard `.so` 与 Unity 基线逐字节一致，APK 中没有 libunity.so。build-info.json 保存在 APK 同目录。

`native-final-smoke-20261023-133935` 完整采集约 15.344 秒，可靠呈现覆盖约 14.346 秒（93.50%），871 个相邻间隔，无未知历史缺口。均值 16.471 ms，P99 24.711 ms，最长 24.774 ms；±25% 之外的节奏异常比例 28.93%，按工程门槛判为 FAIL。这是采集链路自检，没有控制真实转动轨迹，不能拿它与其他时间的 Unity 运动数据直接评定体验优劣。

当前设备此 EGL Surface 路径的能力检测记录 `presentSupported=0`，没有可用的 EGL 显示时间。原生 CSV 的 present=0 被正确排除，没有输出伪造的延迟数字。SurfaceFlinger 的实际呈现数据可正常采集。日志、截图、配置副本及实机验证辅助脚本在 `D:/visor-x/output/native-cardboard/`。

量化分析的 7 项测试通过，包含完整稳帧、平均 60 FPS 但不均匀、历史缺口、提前结束、重复/无效时间戳、CSV 缺失/截断行、刷新周期变化。当前手机最终留在真实 IMU 模式的原生应用，Unity 安装及工程配置保留。

## refresh-v2 验证说明

本轮修改包含显示模式请求和按实际刷新周期调度两个紧密相关的部分，结果不能单独证明其中哪一部分贡献了改善。保留的 v1 APK 位于 `D:/visor-x/output/native-cardboard-refresh-v2/baseline-v1.apk`。这次变更仅应用于原生对照应用。

`native-refresh-v2-smoke-20261023-151734` 的实机预检中，模式从 120 Hz 在约 3 秒后切换到 60 Hz；15 秒采集可靠覆盖 99.70%，908 个间隔，P99 16.511 ms、最长 16.512 ms、节奏异常 0%，工程判定 PASS。该样本为功能自检，未控制用户的头部运动，正式改善结论以用户后续 30 秒运动采集为准。EGL 呈现时间仍不可用，未测得物理运动到光子延迟。

Java 策略测试覆盖 59.94、60、90、119.88、120、144、165、240 Hz 和回调遗漏；分析器 9 项测试包含刷新率中途变化、72 FPS 的门槛处理。

## prediction-v3：姿态预测单变量对照

用户已确认 v2 基本不再卡顿，但转头时场景略微同向跟随、停止后逐渐稳定。此症状提示动态跟随误差，尚不能确认是预测不足、过量还是其他时序问题。v3 保留 v2 刷新率策略，仅将姿态预测偏移参数化；默认仍为 50 ms。先测试 25 ms，用变化方向判断下一步，不预设降低预测一定改善延迟。不要同时更改 neck model、投影、畸变、姿态映射或刷新率策略。

启动 25 ms 实验（每次换参数均先 force-stop，避免向已有 Activity 发送参数而未生效）：

```powershell
adb -s 10.168.2.127:43495 shell am force-stop com.spatialai.samples.cardboardnative
adb -s 10.168.2.127:43495 shell am start -n com.spatialai.samples.cardboardnative/.MainActivity --ei predictionMs 25
```

恢复 50 ms 对照：同样先 force-stop，再以 --ei predictionMs 50 启动。无参数冷启动默认 50 ms；退出再从桌面重开可能回到默认值，采集前核对终端 Pose prediction。允许 0–100 ms，越界值回落到 50 并警告。0 ms 指查询当前 BOOTTIME 的姿态，融合内部仍会从最新传感器时刻外推到当前时刻，不等于完全禁用所有外推。

DISPLAY_RATE、CONFIG、WINDOW 和 refresh-state.json 都记录 predictionMs。CSV 新增 prediction_ms 与 pose_query_boot_ns；同一 BOOTTIME 时钟的 pose_target_boot_ns - pose_query_boot_ns 可核对请求预测量，不能当作物理延迟。每帧仍只查询一次姿态，未增加额外姿态滤波。

试验时先触摸归正并静置 10 秒，再执行 30 秒采集；期间依次左右转头、抬头低头、停下，每次动作保持接近上一版。反馈同向跟随是否减轻、是否出现反向漂移、停止后是否有回弹和收敛过程。

## present-v4：按预计呈现时刻预测

本轮只验证姿态目标时间。API 33+ 的固定和动态模式都使用同一 VsyncCallback 调度，复制 preferred FrameTimeline 的预计呈现时间、deadline、vsyncId，与回调时间组成不可变快照，再交给 GL 线程。不能把 FrameData/FrameTimeline 对象留到回调外读取。刷新策略、原库融合、光学和场景参数沿用 v3。

动态模式在每帧姿态查询前紧邻采样 MONOTONIC / BOOTTIME / MONOTONIC，以两个 MONOTONIC 的中点对应 BOOTTIME：

```text
targetBoot = queryBoot + (expectedPresentMono - queryMono)
```

它不是简单把刷新周期加到 now，也不从固定 50 ms 上再叠加预测。deadline 已过而呈现时刻尚未到达时仍使用该呈现时刻，另记 deadline miss；预计呈现时刻已经过期时，将目标钳制到当前查询时刻并记 `expired_clamp_zero`。缺少 timeline（含 Android <33）或时间关系无效/提前超过 100 ms 时，明确记录 `fallback_missing` / `fallback_invalid`，回退到 predictionMs。默认冷启动仍为 fixed / 50 ms。

启动本轮动态模式：

```powershell
adb -s 10.168.2.127:43495 shell am start -S -n com.spatialai.samples.cardboardnative/.MainActivity --es predictionMode display-time --ei predictionMs 50
```

启动固定 50 ms 对照：

```powershell
adb -s 10.168.2.127:43495 shell am start -S -n com.spatialai.samples.cardboardnative/.MainActivity --es predictionMode fixed --ei predictionMs 50
```

每次启动后等待至少 10 秒，再采集；不要在同一轮中切换模式。动态采集命令：

```powershell
python -X utf8 tools/CaptureImuPacing.py --package com.spatialai.samples.cardboardnative --label native-present-v4 --seconds 30
```

每 5 秒 `POSE_TIMING` 输出实际请求提前量均值/范围、过期/回退次数、CPU 绘制结束超 deadline 次数。CSV 新增 prediction_mode/source、effective_prediction_ns、expected_present_mono_ns、deadline_mono_ns、vsync_id、request_sequence、refresh_generation、draw_start_mono_ns、clock_sample_span_ns 和 query/draw_deadline_missed。v4 的 pose_query_mono_ns 是实际查询前的时钟配对中点；旧版该列是在 draw 开始处采样。prediction_ms 在动态模式下仅表示固定回退值，实际提前量看 effective_prediction_ns。

分析器仅统计当前采集时间窗口内、warmup 后的行；输出提前量分位数、过期/回退分布、时钟换算一致性。CPU deadline 检查不包含 GPU 完成、eglSwapBuffers 或 SurfaceFlinger 提交。预计呈现时间不等于本帧真实呈现：此版本尚未将 timeline/vsyncId 绑定到 EGL buffer，系统仍可能把 buffer 放到其他显示周期。该手机的 EGL presentSupported=0 时仍不能据此量出真实运动到光子延迟；必须结合帧节奏和用户运动反馈评价实验。

验证：`tests/ImuPacing/pose_timing_test.cpp` 覆盖固定、动态、迟到、过期、缺失、无效和休眠时钟偏移；`tests/ImuPacing/test_analysis.py` 覆盖无 EGL 实际呈现时刻时的预测诊断、采集窗口过滤和异常换算检测。

2026-10-23 实机预检：C++ 9 个时序用例、Python 10 个分析用例通过，v4 编译、签名校验、安装成功。APK SHA256 `9dac22760a354a0c4080bb9ee0b20c2a9462dad434b0d64b03c8c6427b59893f`，归档 `D:/visor-x/output/native-cardboard-present-v4/spatial-cardboard-imu-present-v4.apk`。

- `native-fixed-v4-smoke-20261023-163529`：877 个呈现间隔，P99 16.511 ms，异常 0%，PASS；877 行提前量全部 50 ms。
- `native-present-v4-smoke-20261023-163709`：854 个已知呈现间隔，P99 16.471 ms，异常 0%；存在 65.87 ms 未知历史缺口，完整验收 INCONCLUSIVE。858 行动态提前量 P50 24.165 ms / P95 25.150 ms，范围 20.986–25.537 ms，无回退、过期、CPU deadline miss 或时钟换算错误。
- 补采 `native-present-v4-check-20261023-163805`：721 个已知间隔 P99 16.510 ms，异常 0%；采集历史仍有缺口且覆盖仅 79.07%，完整验收 INCONCLUSIVE。826 行动态提前量 P50 23.831 ms / P95 24.929 ms，范围 21.868–25.504 ms，仍无回退、过期、CPU deadline miss 或换算错误。

上述为功能/日志预检，不是受控运动体验验收。缺口没有被算成真实长帧，也未因已知样本正常就给整轮 PASS。手机留在 display-time 模式，待用户运动采集；从桌面重新冷启动可能回到 fixed，采集前核对终端显示的 prediction mode。

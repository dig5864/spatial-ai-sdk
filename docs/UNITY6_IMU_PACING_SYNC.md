# Unity 6 IMU 帧率面板与残留卡顿排查

2026-10-26，对照 SDK 提交 `8b9a35d` 及其 IMU 优化记录，检查 Unity 6000.6.2f1 URP Demo。

## 已确认的问题与修改

Unity 6 生成器之前只复制 IMU 场景脚本，没有复制 `SpatialFrameRateDebugControls`，也没有像原 Demo 的 `SpatialXRSceneControl` 那样初始化该组件。因此 SDK 校准齿轮存在，Demo 帧率面板缺失。已补齐脚本复制、新场景挂载、已有 IMU 场景补挂，并同步到 `D:/code/unity/VisorX-Demo-unity6`。

面板只负责 UI 和 PlayerPrefs，调用同一 SDK 的 `SpatialFrameRate.Configure`、读取 `Status`。支持 FollowDisplay / Locked60 和 2–4 倍软件余量。Android Optimized Frame Pacing 显式关闭，与原 SDK Demo 一致；修复前该工程也已经关闭，此项不是此次掉帧的已证实原因。

采集工具移除了只识别原 Demo 包名的限制，使 Unity 6 自定义包名也能采集 `refresh-state.json` 和 XR 姿态时序 CSV。

## 原生优化没有漏打包

旧 Unity 6 APK 与 SDK `libGfxPluginCardboard.so` 的 ELF Build ID 相同：`2818e0467d51574a760ede6e6d62b2a7f410f0f7`。APK 中存在 `SpatialFrameTiming_nativeConfigureCadence` 和 `nativePublish`，Java AAR 及原生源码校验值与本机 `build/native/unity-imu-timing/build-info.json` 一致。APK strip 后二进制整体 SHA 不同，不能据此认定插件过期。

旧 APK 备份：`build/validation/unity6-compat/imu-sync-audit/Spatial-XR-Unity6-before-fps-ui.apk`。

## 真机基线

设备 Xiaomi 2206123SC（mayfly），测试包 `com.spatialai.compatibility`，Unity 6000.6.2f1。采集期间保持 FollowDisplay、2x，系统实际约 60 Hz，目标约 60 FPS、软件上限 120，显示时间预测已生效。

这与先前原 SDK Demo 的 120 Hz / FollowDisplay / 软件上限 240 条件不同。SDK 不强制系统使用 120 Hz，软件上限不是实测输出帧率。先前报告也明确指出锁 60 模式仍未通过节奏验收。

30 秒基线（有效覆盖 30.725 秒，1801 个显示间隔）：

| 指标 | 结果 |
|---|---:|
| 显示间隔 P50 / P99 | 16.461 / 32.924 ms |
| 最长间隔 | 33.005 ms |
| 长间隔 | 65（3.609%） |
| 大于 50 ms | 0 |
| BeforeRender 重复目标时间 / 大步进 | 12 / 37 |
| 姿态预测来源 | 全部 display_time |

原始数据：`build/validation/imu-pacing/unity6-before-fps-ui-20261026-161230/quantitative.md` 和同目录 CSV。显示间隔未通过本项目节奏门槛，证实残留不均匀呈现；目标时间连续性是诊断信号，不等同于实测四元数抖动或运动到光子延迟。没有受控转头 A/B，不能仅据此判定 Unity 6 比 Unity 2022 更差。

## 验证范围

Unity 6000.0.84f1 Built-in 和 6000.6.2f1 URP 的 Play 检查通过：场景含帧率组件、Locked60/3x 与 FollowDisplay/2x 调用 SDK 生效、Input System R 键归零、参照物渲染正常。帧时序分析测试 15 项通过。

补面板恢复了配置入口，不代表消除了上述残留掉帧。实际屏幕刷新率、真实呈现间隔和目标时间应分别核对。

新 APK 完成面板切换并还原 FollowDisplay / 2x 后，独立 30 秒采样通过：1847 个间隔、长间隔 0、P99 16.476 ms、最长 16.515 ms。日志确认 settingsOpen=false、XR 正常，截图确认场景已恢复渲染。数据位于 `build/validation/imu-pacing/unity6-after-fps-ui-20261026-163209/`。这轮结果优于旧包，但同时经历了重启和策略切换，尚不能把改善单独归因于补挂 UI，也不能据静置/非受控运动采样宣称转头卡顿已解决。

随后以保存的 FollowDisplay / 2x 强制停止并重新启动应用，不再切换面板，第二轮也通过：1853 个间隔、长间隔 0、P99 16.512 ms、最长 16.514 ms，覆盖 30.520 秒。数据位于 `build/validation/imu-pacing/unity6-follow2-restart-20261026-163345/`。短时呈现结果已改善；受控运动体感和更长时段验收仍未完成。

6000.6.2f1 APK 已重新构建并安装到同一测试手机。齿轮内面板实际显示正常，点击 Locked60 / 3x 后诊断状态为 locked60、headroom=3、softwareCap=180、modeRequestApplied=true；重启后保留。验证后恢复 FollowDisplay / 2x。截图与状态证据位于 `build/validation/unity6-compat/imu-sync-audit/`。新 APK：`build/validation/unity6-compat/6000.6-urp/Builds/Android/Spatial-XR-Unity6.apk`。

## 显示管线补充采样

补面板前的独立 Perfetto 采样位于 `build/validation/imu-pacing/unity6-60hz-pipeline-20261026-162541/`。该轮 30.762 秒显示间隔中有 73 个长间隔（4.067%，P99 32.929 ms），复现同类问题。完整约 60 秒 trace 中，3588 次 EGL swap 的平均提交间隔 16.690 ms、最长 26.447 ms；swap 调用平均 0.652 ms、最长 3.044 ms；Swappy 的 GPU frame time 平均 3.198 ms、最长 7.112 ms。姿态查询平均 0.028 ms、最长 0.702 ms。

这些数据暂不支持“GPU 渲染超出 16.7 ms”或“姿态查询本身耗时过长”的解释，更指向提交相位及显示时间线节拍。但 trace 没有该 SurfaceView 的完整 App FrameTimeline，不能仅凭统计断言根因，更不能直接把 Cardboard/Unity 内部的 Swappy trace 与 PlayerSettings 的开关等同。下一步应在同一实际刷新率、同一场景和策略下对照 Unity 2022.3 与 Unity 6 的提交、fence 和姿态目标时间，不应把加大软件余量当成已证实修复。

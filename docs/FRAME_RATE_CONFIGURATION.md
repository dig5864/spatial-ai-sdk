# SDK 帧率配置

Android Cardboard 的运行时帧率配置位于 `SpatialAI.Cardboard` 命名空间。两种策略由同一个 APK、同一套 SDK 执行，不需要编译宏或 Demo 单独计算帧率。

## API

```csharp
using SpatialAI.Cardboard;

// 跟随实际显示刷新率；不强制屏幕使用 120 Hz。
SpatialFrameRate.Configure(SpatialFrameRateMode.FollowDisplay);

// 请求 60 Hz，并独立控制输出为最多 60 FPS。
SpatialFrameRate.Configure(SpatialFrameRateMode.Locked60);

// 软件余量范围 2~4，默认 2。无效数值抛出 ArgumentOutOfRangeException。
SpatialFrameRate.Configure(new SpatialFrameRateConfiguration(
    SpatialFrameRateMode.FollowDisplay, softwareHeadroomMultiplier: 3f));

var requested = SpatialFrameRate.Configuration;
var actual = SpatialFrameRate.Status;
bool applied = actual.IsActive &&
    actual.ConfigurationVersion == SpatialFrameRate.ConfigurationVersion;
// actual.DisplayRefreshHz：实际生效的系统显示模式刷新率。
// actual.TargetFrameRate：策略目标，不是实测呈现 FPS。
// actual.SoftwareFrameRateCap：Unity 软件帧率上限，不是输出 FPS。
```

请在 Unity 主线程调用 `Configure`。SDK 自动启动一个跨场景控制器，配置跨场景保持。SDK 不写应用偏好；应用可自行保存。`SpatialCardboardBridge.frameRateConfiguration` 提供初始 Inspector 参数，显式 `Configure` 调用优先。`useDisplayTimedFramePacing` 控制 Bridge 是否自动初始化；显式调用 `Configure` 仍会启动 SDK 控制器。非 Android/Editor 可保存和校验配置，但不执行 Android 显示控制，Status 不会被标记为 active。

## 策略和余量

- `FollowDisplay`：目标随实际 60/90/120/144 等显示模式变化。恢复进入 SDK 前的窗口刷新率偏好，移除 SDK 锁 60 的覆盖；系统仍决定最终模式。
- `Locked60`：优先请求同分辨率 60 Hz 模式，支持回退到其整数倍模式。独立在 XR `PopulateNextFrameDesc` 按 VSync 放行，系统升到 120 Hz 时不会因为软件上限有余量而直接输出 120 帧。90/144 等非整数倍模式采用固定分数比选取 VSync，平均目标为 60；物理刷新周期不能整除时，呈现间隔无法完全相等。显示低于 60 Hz 时目标不超过显示能力。
- 软件上限按**实际显示刷新率乘余量**计算并向上取整，不按锁 60 的目标计算。默认例子：60 Hz→120，120 Hz→240，144 Hz→288。Locked60 在 120 Hz 下仍使用 240 的软件上限，避免将软件限帧再次放到显示节拍附近。
- 后台查询模式变化，VSync 回调只发布时间线。配置变更和暂停恢复重新发布 generation 与 configurationVersion，C# 仅应用匹配版本的状态。停止控制器会恢复之前的 Unity 软件上限和窗口偏好。

余量用于避免已发现的软件限帧与显示节拍冲突，不能保证消除渲染负载、GPU/系统合成、传感器或呈现相位造成的所有卡顿。目标和上限不能代替实际呈现测量。

## Demo

统一构建入口 `Spatial AI/Build Android XR Sample`，生成 `spatialxr-sdk-frame-rate-settings.apk`。打开右上角齿轮，左侧 `Frame rate (SDK)` 面板可选择 `Lock 60 FPS` 或 `Follow screen refresh`，并选择 2x/3x/4x 余量。

Demo 只调用 `SpatialFrameRate.Configure` 和读取 `Status`，偏好由 Demo 的 PlayerPrefs 保存，场景切换和重启后重新通过 SDK 应用。各场景不再直接设置 `Application.targetFrameRate`。

开发广播同样先进入 Demo 再调用 SDK：

```powershell
adb -s 10.168.2.127:42585 shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE --es scene imu --es frameRateMode Locked60 --ef frameRateHeadroom 2
adb -s 10.168.2.127:42585 shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE --es scene imu --es frameRateMode FollowDisplay --ef frameRateHeadroom 3
```

`SpatialUnityTiming` 的 DISPLAY_STATE、CADENCE_WINDOW 和 Unity 的 `SpatialAI FRAME-RATE` 分别记录实际模式/软件上限/配置版本、原生节奏、SDK 应用结果。`refresh-state.json` 保留实际模式和目标，采集工具可继续读取。

## 本机验证（2026-10-26）

Xiaomi mayfly 当前实际模式保持 120 Hz，系统未采纳窗口的 60 Hz 请求。以下是每组约 15 秒的静置呈现采样，不能代替转头体感和运动到光子延迟测量。

| 策略 | 软件上限 | 样本间隔数 | P99 呈现间隔 | 节奏异常比例 |
|---|---:|---:|---:|---:|
| FollowDisplay，2x | 240 | 1796 | 8.257 ms | 0% |
| Locked60，2x | 240 | 940 | 24.704 ms | 4.36% |
| Locked60，4x | 480 | 926 | 24.700 ms | 2.38% |

跟随模式通过本项目的节奏门槛；锁 60 模式在 120 Hz 屏幕下仍有长短帧配对，未通过。提高余量后依然存在异常，不能将其归结为软件上限不足，也不能宣称锁 60 的卡顿已修复。默认保持 FollowDisplay、2x，Locked60 保留用于对照测试。API 切换、非法参数拒绝、Demo 保存/重启恢复、跨场景和暂停恢复已验证。

原始采集目录位于 `build/validation/imu-pacing/sdk-followdisplay-margin2-20261026-130109`、`sdk-locked60-margin2-20261026-130130`、`sdk-locked60-margin4_0-20261026-130155`，构建产物和采集文件不纳入提交。

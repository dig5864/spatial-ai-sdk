# 多手机屏幕适配：使用与验证

日期：2026-10-13。实现范围：Android 全屏横屏左、固定平面支架、现有 RhinoX 参考光学显示路径。

## 行为

SDK 读取 Android Activity 对应 Display 的完整 metrics、各轴 DPI、显示模式、窗口尺寸和方向，并与 Unity 输出核对。优先使用本机保存的实测屏幕尺寸，否则以 Android `xdpi/ydpi` 估算有效屏幕宽高。不会使用逻辑 `densityDpi` 或 Unity `Screen.dpi` 作为物理尺寸。

固定支架的两眼区域保存为相对设计屏幕中心的毫米矩形；运行时根据手机有效宽高、屏幕中心安装偏移和当前输出像素尺寸换算。降低输出分辨率不会改变物理区域；降低每眼渲染纹理分辨率也不修改支架配置。

当前小米 `referenceEyeRects` 保留。尚未建立支架配置时，该参考手机可用系统 DPI 临时推导估算布局，在原分辨率下复现原矩形；其他手机必须先取得参考支架配置，不能从未知手机屏幕尺寸猜测固定光路。

## 第一次建立支架基准

1. 在已配置小米参考矩形的手机运行 Display and 3DOF 场景或独立测试 APK，保持全屏横屏左。
2. 点击屏幕右上角 **齿轮** 打开屏幕尺寸校正（Android 原生点击区域覆盖 Cardboard 齿轮，避免被 XR 畸变输出遮挡；已移除顶部中央的大按钮）。此时暂停 XR 双目输出并隐藏场景内容，在裸屏上显示校正界面。
3. 用真实尺子分别核对水平和竖直 50 mm 线段；调整 Active width/height mm，直到两条线均为 50 mm，也可直接填写有效发光区域实测宽高。尺寸不含机身边框。
4. 若有效屏幕中心与支架设计中心对齐，两个 offset 填 0；否则填屏幕中心相对支架设计中心的偏移，右/下为正。确保屏幕平面和倾角与参考安装一致。
5. 选择 **Save + establish reference rig**，保存手机测量和支架毫米矩形；然后 Close 恢复双目显示。此操作适用于参考手机，不能用另一台手机重新建立相同支架来掩盖布局误差。

只选择 **Save measured screen** 会更新该手机尺寸/安装偏移，不改已有支架矩形。SDK 不会自动把系统估算值保存为 measured，也不会把屏幕测量标记当作光学标定通过。

## 将同一支架用于其他手机

支架配置导出在：

```text
Application.persistentDataPath/spatial-screen/rig.json
```

独立测试 APK 包名为 `com.spatialai.validation.screen`，常见文件位置：

```text
/sdcard/Android/data/com.spatialai.validation.screen/files/spatial-screen/rig.json
```

用 `adb pull` 取回文件，放入应用的 `Resources/SpatialAI/ScreenRig.json` 并重新构建；所有手机就可自动加载同一支架配置。也可以赋给 `SpatialCardboardBridge.phoneRigProfile`，或由应用自身写入本地 `spatial-screen/rig.json`。手机之间不会自动同步本地文件。避免用 ADB 在应用首次创建校正目录前执行 `mkdir`：Android 16 真机上该目录会属于 shell，应用无法读取或写入；跨手机部署优先使用 Resources/显式配置。

加载优先级：显式 `phoneRigProfile` → Resources `SpatialAI/ScreenRig` → 本地 `rig.json`。显式/Resources 配置存在时，本地文件不覆盖它；如需重测支架，使用没有绑定预置配置的参考测试应用重新导出，然后替换项目资源。

新手机读取自身 DPI 估算尺寸；有误差时同样校正两条标尺，选择 **Save measured screen**。尺寸文件按厂商/型号/deviceCode 隔离，不保存永久 px/mm，因此显示模式变化后仍根据新输出分辨率计算。

未知型号在明确的支架配置下使用外接 UVC 来源和支架中保存的显示补偿/候选手机 IMU 安装映射。精确型号匹配优先；RhinoX 一体机和外接眼镜身份不使用这一后备配置。复用 IMU 候选映射不表示新手机追踪已验收，仍应核对三轴转动。

## 运行时变化和限制

- 每秒检查屏幕参数；输出像素尺寸变化立即重算，校正保存或恢复前台时强制重读。
- 原生接口一次提交网格、视锥、屏幕尺寸和两眼矩形；查询已应用 revision，避免误报旧配置已生效。
- 拒绝无法确认映射的窗口/分屏、错误方向、区域越界、原生左右半屏限制和 safeArea 遮挡。不会静默缩小区域或整体平移来避开刘海。
- 缺少尺寸时可手动测量；缺少支架基准时提示导入 rig。关闭校正界面后按最新有效布局恢复显示。
- 校正界面要求裸屏尺子测量。屏幕偏移不替代发光平面高度、倾角或眼位标定；曲面屏和改变光路的安装不在当前支持范围。
- 当前网格与视锥仍为 RhinoX 参考，未建立这套反射支架的完整光学标定。布局 MEASURED 仅指当前手机和参考手机的物理尺寸已由用户确认。
- 尚未内置已测量的机型尺寸数据库；初始参考毫米参数通过 API 估算或用户校正建立，没有编造小米的实测尺寸。

## 开发入口

| 文件 | 职责 |
| --- | --- |
| [ScreenLayout.cs](../packages/com.spatialai.sdk/Runtime/Core/ScreenLayout.cs) | 物理尺寸/支架契约、参考矩形转 mm、不同手机动态 AA 计算和越界校验 |
| [SpatialPhoneScreen.cs](../packages/com.spatialai.sdk/Runtime/Unity/SpatialPhoneScreen.cs) | JNI 读取、实测尺寸优先级、按机型保存、支架 JSON 加载/导出 |
| [SpatialScreenCalibration.cs](../packages/com.spatialai.sdk/Runtime/Cardboard/SpatialScreenCalibration.cs) | 裸屏双轴 50 mm 校正界面 |
| [SpatialCardboardBridge.cs](../packages/com.spatialai.sdk/Runtime/Cardboard/SpatialCardboardBridge.cs) | 动态配置、暂停校正、恢复、应用状态与诊断日志 |
| [ValidatePhoneScreen.ps1](../tools/ValidatePhoneScreen.ps1) | 隔离 Unity 项目及独立 Android 测试 APK 构建 |
| [ScreenLayoutChecks.cs](../tests/CoreChecks/ScreenLayoutChecks.cs) | 不同像素/物理尺寸、安装偏移、参考布局回归与异常参数测试 |

开发验证命令：

```powershell
dotnet run --project tests/CoreChecks/CoreChecks.csproj
powershell -NoProfile -ExecutionPolicy Bypass -File tools/BuildRhinoXCardboard.ps1 -NdkPath '<Android NDK 路径>'
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ValidatePhoneScreen.ps1 -UnityPath '<Unity 2022.3 Editor 路径>'
# 用已经确认的固定支架测试新手机：只向隔离测试项目导入资源。
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ValidatePhoneScreen.ps1 -RigPath docs/calibrations/mayfly-fixed-rig/rig.json
```

后续完整光学标定、左右眼独立投影与兼容矩阵规划见 [设计文档](PHONE_SCREEN_ADAPTATION_DESIGN.md)。

## 本轮验证记录

- Core：1012 项检查通过，包含新增物理布局回归。
- Android 条件代码参考编译：0 错误、0 警告；Unity 2022.3.14f1 Android IL2CPP 独立 APK 构建通过。
- 修改版 Cardboard 原生库构建和新接口符号检查通过；库 SHA-256 为 `acf4b6a46dac36969e119b74db7554175a53b375d47a7b53b43e5d00336f8fb5`。
- 独立 APK：`build/samples/android/spatial-screen-adaptation.apk`；构建日志位于 `build/validation/phone-screen-unity/verification.log`。
- 已连接 Xiaomi 2206123SC / mayfly，Android `wm size` 为 1080×2400。`wm density=440` 是逻辑密度，不能作为本轮物理尺寸测量。
- 用户手动安装后完成真机验证；修复校正入口被 XR 输出遮挡的问题后，更新 APK 通过 `adb install -r` 安装成功。
- Android `xdpi=419.449`、`ydpi=419.257`，初始估算有效屏幕为 145.3335×65.43004 mm。用户用尺核对两条线，确认均约为 50 mm；保存尺寸为 145.334×65.430 mm。此为用户标尺确认，显示的小数位数不代表测量精度。
- 选择 Save + establish reference rig 后，手机尺寸与支架毫米区域均成功保存；再次 Save measured screen 覆盖已有尺寸文件也成功。关闭校正恢复双目，原生 applied revision 确认配置实际生效。进程重启后自动加载保存值，状态为 `MEASURED layout; reference optics UNVERIFIED`。
- 输出 2400×1080 时两眼矩形为 `268.22,157.045,865.054,865.054` 与 `1266.727,157.045,865.054,865.054`，复现原参考布局。
- 测试 APK 通过 Intent extra `screenTestOutputPercent=80` 将应用 Surface 降到 1920×864；两眼矩形变为 `214.576,125.636,692.0432,692.0432` 与 `1013.382,125.636,692.0432,692.0432`，正好为原来的 80%。原生 revision=2 已应用，截图中物理区域位置保持一致。测试未修改系统 `wm size/density`。
- 校正界面双轴标尺完整可见，场景内容隐藏；恢复前台后重新应用布局。测试结束已恢复默认 2400×1080 输出。
- 本次实际导出的 [支架配置](calibrations/mayfly-fixed-rig/rig.json) 与 [手机尺寸](calibrations/mayfly-fixed-rig/phone.json) 已保留，来源和使用方式见 [说明](calibrations/mayfly-fixed-rig/README.md)。可将 rig 导入使用同一支架的其他手机应用；没有默认加载到所有 SDK 项目。
- 第二台小米的验证见下方。系统显示模式切换、镜片后光学效果及 IMU/UVC 联动尚待实机验收。

## 第二台手机验证：Xiaomi 24129PN74C / dada

2026-10-13，Android 16，通过 Wi-Fi ADB 验证。使用上一台 mayfly 导出的同一份支架配置，导入独立测试 APK 的 Resources；没有创建新型号专用像素矩形。

- 原生屏幕 1200×2670，横屏输出 2670×1200。`xdpi=459.7285`、`ydpi=460.4073`，系统估算有效屏幕 147.5175×66.20225 mm。
- 用户确认水平、竖直 50 mm 标尺均约为 50 mm，手动 Save measured screen 后保存 147.518×66.202 mm。应用自行创建的校正目录可正常保存；重启自动读取 `measured=True`。
- 未知型号正确匹配显式支架的手机后备配置，原生 applied revision 确认布局已应用。双目截图与约 60 FPS 运行日志正常，状态为 `MEASURED layout; reference optics UNVERIFIED`。
- 2670×1200 输出的两眼矩形为 `313.7417,179.4564,948.1247,949.9626` 与 `1408.135,179.4564,948.1247,949.9626`。矩形根据本机尺寸与固定支架毫米区域计算，并非只按上一台分辨率缩放。
- 输出降到 2136×960（80%）后，两眼坐标和宽高也变为 80%，原生配置应用成功；保存后的进程重启与低分辨率输出均通过。测试结束恢复 2670×1200，没有修改系统 `wm size/density`。
- 该手机限制 ADB 注入点击，校正入口、尺子核对和保存/关闭由用户手动操作。初次 ADB 创建目录造成的权限问题已移除，使用打包支架资源完成验证。
- 用户随后将 dada 安装到支架，确认镜片上显示正常。这记录本次安装下的观察结果；参考网格与完整光学/眼位测量尚未标定，因此不修改 `opticsValidated`。
- [本机保存尺寸](calibrations/dada-fixed-rig/phone.json)；运行日志、截图在 `build/validation/phone-screen-dada/`。完整光学/眼位测量、安装偏移精度以及 IMU/UVC 联动仍未验收；屏幕测试包日志 `rotationValid=False`，不能据此宣称 3DoF 正常。

## 完整 Demo 供多设备 IMU 验证

同日按用户要求将同一支架资源加入 `samples/unity/SpatialXR/Assets/Resources/SpatialAI/ScreenRig.json`，沿用 `com.spatialai.samples.xr` 和默认 `ImuDiagnostics` 启动场景。Unity Android IL2CPP 构建成功，输出 `build/samples/android/spatialxr.apk`，已安装到 dada；构建日志 `build/logs/spatialxr-phone-rig.log`，启动截图和运行日志位于 `build/validation/spatialxr-phone-rig/`。

完整 Demo 的 IMU 场景正确读到 `Pose: XR CenterEye`、`CenterEye valid=True`、`Input HMD=True`，原始 Cardboard/映射 XR 四元数以及手机陀螺仪、加速度均有运行数据。`Head valid=False` 时现有诊断逻辑回退到有效 CenterEye，屏幕测试包仅查询 Head 的 `rotationValid=False` 不能代表完整 Demo 没有姿态。三轴方向、安装映射和长时间漂移仍由用户进行多设备实机验证，保留 `MODEL UNVERIFIED`。

Demo 的手机校正数据与独立屏幕测试应用隔离，首次打开使用系统 DPI 估算；需要持久保存时在每台手机的 Demo 内核对标尺后选择 Save measured screen。支架资源统一复用，不在新手机重新建立参考支架。

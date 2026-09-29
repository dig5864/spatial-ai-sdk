# Spatial XR Unity 消费工程

这是独立的 Unity 2022.3.14f1c1 基线工程，使用相对路径导入当前 Spatial AI UPM 包和固定版本 Cardboard XR Plugin。Unity 6 与团结引擎仍需各自导入测试。

1. 在 Unity Hub 中打开本目录，等待 Package Manager 导入依赖。工程已有导入的 XR Sample 和 `Assets/Scenes/HelloSpatialXR.unity`。
2. 如需重新建立场景，菜单 `Spatial AI > Create Cardboard XR Sample Scene`，保存场景；通过 `Spatial AI > Configure Cardboard Android XR` 或 `Configure Cardboard iOS XR` 配置对应平台的 XR Loader。
   现有场景的 `XR Camera` 是普通 Unity Camera 加 Input System 的 `Tracked Pose Driver`，由 Cardboard XR Provider 提供双眼渲染与头姿输入。若编辑器打开期间未刷新到最新场景，执行 `Spatial AI > Repair XR Sample Camera`，保存场景；无需运行 Unity 的“Upgrade to XR Rig”菜单。
3. Android 构建入口为菜单 `Spatial AI > Build Android XR Sample` 或方法 `SpatialXRSetup.BuildAndroidSample`，成功时输出到 SDK 的 `build/samples/android/spatialxr.apk`。工程为 Unity 2022 固定 Cardboard XR Plugin v1.30.1（UPM 包版本号 1.30.0）；脚本设置 ARM64、OpenGLES3、横屏和 Cardboard Loader。`SpatialXRAndroidGradle` 为此示例加入 Cardboard 所需 AndroidX 依赖，并使用 Android SDK Platform 34 / Build Tools 30.0.3。当前机器会优先读取 `SPATIALAI_ANDROID_SDK_ROOT`、`ANDROID_SDK_ROOT` 或 `ANDROID_HOME`，再使用 `D:\AndroidSdk\sdk`；其他机器应设置 `SPATIALAI_ANDROID_SDK_ROOT`。这些配置仅针对 Unity 2022 示例，Unity 6 与团结需分别验证。

   Windows 上也可直接双击工程根目录的 `BuildAndroid.bat`。它会检查 Unity 版本、Android SDK 和场景；如果工程已在 Unity 打开，就请求该编辑器构建；否则启动对应版本的 Unity，等待其导入并构建。首次新增脚本后若一直显示“等待编辑器”，请切回 Unity 窗口一次，待脚本导入完成。窗口会显示结果，详细日志保存在 SDK 的 `build/logs/`。仅检查环境可运行 `BuildAndroid.bat -CheckOnly`。脚本不会自动安装到设备。
4. iOS 构建入口为菜单 `Spatial AI > Build iOS Xcode Sample` 或编辑器方法 `SpatialXRSetup.BuildIosSample`。在 macOS 上安装 iOS Build Support 和 Xcode 后运行，输出到 SDK 的 `build/samples/ios/spatialxr-xcode/`；再在 Xcode 中配置签名，安装到 iPhone。脚本设置横屏、Metal、IL2CPP、iOS 12.0 最低版本以及扫描 Cardboard Viewer 二维码所需的相机用途说明。此路径未在本机运行。
5. 安装到测试设备后打开 `SpatialXRSample`：画面应出现双眼视图、状态文字和正前方的青色方块。转头观察视图是否跟随；注视方块时它变黄，持续约 1.5 秒后变绿，`Hits` 增加；Cardboard Viewer 扳机也可触发。状态文字分别显示 XR display、Viewer 参数、头姿和触发次数。可用 `adb -s 03P11D14106Y shell am start -n com.spatialai.samples.xr/com.unity3d.player.UnityPlayerActivity` 启动当前测试设备上的应用。当前没有完成 RhinoX 光学验收。

此示例在 Android 没有保存 Viewer 参数时，会自动载入 Cardboard I/O 2015 **通用测试参数**，以便先验证画面、头姿和注视交互；画面标记 `GENERIC TEST ONLY`。该参数与 RhinoX Pro 的镜片、屏幕位置及 OST 畸变无关，不能用于图像质量或光学验收。拿到真实 Viewer 二维码后，可通过 Cardboard 齿轮入口扫描替换；也可以取消场景根节点 `SpatialCardboardBridge` 的 `Use Generic Viewer For Diagnostics`，要求必须使用真实参数。

Sample 在 Android 读取 `Build.MANUFACTURER`、`MODEL`、`DEVICE`，在 iOS 使用 Unity 报告的手机型号；这都是手机身份。未来外接眼镜的真实 ID 由设备服务填入 `SpatialCardboardBridge.headsetDeviceId`。当前已知的一体机身份 `QUALCOMM / RhinoX / kona` 会精确映射到 RhinoX Pro 历史参考参数，不会仅凭通用 `kona` 代码匹配其他设备。该参数尚未完成硬件校准，因此正式配置校验仍会拒绝。独立 3DoF 手柄尚未接入；iPhone 外接 RhinoX 的显示路由、通信和头姿仍需单独实现。

RhinoX Android 诊断版使用历史 RhinoX Pro 分支的 `Sharp_2k_Seperate` 默认 `gXimmerseAR_WarpMesh` 左右眼网格。首次使用前，在 SDK 根目录运行 `tools/BuildRhinoXCardboard.ps1`，它按固定的 Cardboard v1.30.1 源码和仓库内补丁编译 Android arm64 库，生成 `third_party/rhinox/GfxPluginCardboard-rhinox.aar`，并替换本地 Unity Cardboard 插件的 Android AAR。场景中 `Use Rhino X Legacy Mesh For Diagnostics` 已启用。运行时状态显示 `RhinoX Pro Sharp mesh: applied` 才说明原生渲染器使用了该网格；其他设备继续使用 Cardboard 网格。可在场景根节点取消该选项进行对照。显示生效不等于光学验收，仍需在眼镜中检查双眼对齐、畸变方向和视场边缘。

光学对比场景位于 `Assets/Scenes/OpticsComparison.unity`，可通过 `Spatial AI > Create Optics Comparison Scene` 生成，并通过 `Spatial AI > Build Optics Comparison APK` 构建到 `build/samples/android/optics-comparison.apk`。它与历史工程 `OpticsReference` 使用字节完全相同的 2160×2160 测试图，分别交给两套 XR Runtime 做畸变。两张真机原始截图需要比较每眼方向、中心、网格线与边缘；当前尚未达到光学等效验收。

IMU 调试场景位于 `Assets/Scenes/ImuDiagnostics.unity`。菜单 `Spatial AI > Create IMU Diagnostics Scene` 可重新生成场景，`Spatial AI > Build IMU Diagnostics APK` 构建独立安装包 `build/samples/android/imu-diagnostics.apk`。安装后打开 **Spatial XR IMU Diagnostics**，画面会显示 Cardboard XR 的相对 yaw、pitch、roll、眼中心位置，以及手机陀螺仪角速度和加速度；每秒以 `SpatialAI IMU` 前缀写一条 Android 日志。红 X、绿 Y、蓝 Z 轴在 XR 启动时放到视线前方，之后保持世界方向不动，供 3DoF 转头观察；点按屏幕或使用触发键可将角度归零并把轴重新放到当前视线前方。原始手机传感器值使用设备坐标，XR 姿态使用 Cardboard/Unity 坐标，两者不能直接逐轴等同。真机逐轴转动与符号校验完成前，RhinoX 的 Android IMU 轴映射仍视为未验证。

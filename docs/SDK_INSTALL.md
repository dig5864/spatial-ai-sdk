# Spatial AI SDK 安装与升级（Unity 2022.3）

本交付为 Android 开发预览版。基础 SDK `0.2.0-preview.10`、XR Hands 和 XR Interaction `0.1.0-preview.2`，配套 Spatial 修改版 Cardboard `1.30.0`。应用使用 Unity XR、XR Hands、XRI 标准关节、跟踪和手势事件；CPU/GPU 属于 SDK 后端配置。

## 支持与限制

| 项目 | 当前验证范围 |
| --- | --- |
| Unity | 2022.3.14f1（现有设备使用 2022.3.14f1c1 安装），Built-in Render Pipeline |
| Android | 最低 API 28、ARM64、IL2CPP、OpenGLES3、横屏；编译/目标 API 34 |
| XR 依赖 | XR Hands 1.4.3、XRI 2.5.4、Input System 1.7.0、XR Management 4.4.0 |
| 设备 | Xiaomi 2206123SC / mayfly + HXQ UVC 11cb:281b，固定曝光 16.6ms |
| 跟踪 | 头部旋转 3DOF，手部位置/旋转为单目估算，最多双手 |
| 尚未认证 | Unity 6、团结、URP/HDRP、iOS、其他手机/眼镜、精确手部尺度与光学合相 |

小米光学配置为当前装配的开发参考，不代表所有手机或镜片通用。没有手部标定参数时仅提示并使用预览默认值；标定工具和参数加载保留。默认不关联 USB 插入事件，首次使用及拔插后可能需要 USB 授权；可按第 3.1 节启用设备关联，但不能保证免提示且不自动启动。近距离精确抓取与 6DOF 不在交付范围。

## 1. 准备交付目录和工具

保留交付目录中的 `.tgz`、`release-manifest.json`、`source-file-checksums.json`、`SHA256SUMS.txt` 和说明。可将整个目录拷贝到任意位置。
校验 SHA256SUMS 中各个文件的 SHA-256；发布清单中记录 AAR、模型、原生补丁和源码状态。预览候选可能来自已明确记录的本地修改，不能仅检出 baseRevision 就声称复现同一制品。

Unity Hub 安装 Unity 2022.3 的 Android Build Support、OpenJDK、NDK。Android SDK 还需要安装 `platforms;android-34` 和 `build-tools;30.0.3`。
在 **Edit > Preferences > External Tools** 配置该 SDK；也可设置 `SPATIALAI_ANDROID_SDK_ROOT`。NDK/Java 按 Unity 的工具链配置。
首次导入依赖与 APK 构建需要访问 Unity Package Registry、Google Maven 和 Maven Central；交付包含两个 AAR 与模型，但不是离线 Maven 仓库。
用户不需要检出 SDK/Android/Cardboard 源码，也不需要运行原生编译脚本。

## 2. 新建工程与安装包

新建空的 **3D（Built-in）Unity 2022.3 工程**，只需导入基础 SDK：

1. 打开 **Window > Package Manager > + > Add package from tarball**。
2. 选择 `com.spatialai.sdk-0.2.0-preview.10.tgz`。无需解压、复制目录或执行外部安装脚本。
3. 等待基础 SDK 编译。包内 Editor 安装器自动校验并安装以下三个内置 tgz，再由 UPM 解析 Unity 官方依赖：
   - `com.google.xr.cardboard-1.30.0-spatial.tgz`
   - `com.spatialai.xr-hands-0.1.0-preview.2.tgz`
   - `com.spatialai.xr-interaction-0.1.0-preview.2.tgz`
4. Console 出现 `SpatialAIInstaller: completed` 后，**必须继续第 3 节配置 Android XR**，再按第 4～6 节创建所需场景。仅安装包不会启用双目显示。

基础 SDK 的 package.json 只预先声明可从 Unity Registry 解析的官方依赖；三个本地配套包在 Editor 安装器编译后通过 UPM API 一起注册。内置归档与单独交付的 tgz 完全相同，包含 AAR、模型和样例；首次依赖解析及 APK 构建仍需网络。安装代码仅在 Editor 中编译，不加入 Android APK。
安装器将基础 SDK 自身归档和三个配套归档保留在工程内 `SDKPackages/SpatialAI/<版本>/<交付哈希前12位>/`，设置相对路径引用，保留其他工程依赖。安装前 manifest 备份位于同目录 `manifest-before-install.json`。该目录需要随工程保留/纳入工程版本管理；不能删掉正在引用的 tgz。
安装状态位于 `ProjectSettings/SpatialAISdkInstallationState.json`，支持编译重载和编辑器重启；成功后不重复安装，也不会自动恢复用户主动移除的功能包。安装器不创建场景，不配置 Android XR 或修改应用设置。失败后修正 Console 中的错误，执行 **Spatial AI > SDK Installation > Retry Installation**。
若曾解压过同名 Embedded 包，先将工程 `Packages` 下旧的包目录移到该目录之外并保留备份，再导入基础 SDK。原源码/tgz 引用可由安装器统一更新。如果工程已安装旧的 `com.spatialai.installer`，导入新版基础 SDK 时会移除旧 installer 引用并保留历史归档及备份。

现在默认安装完整四包。纯显示/3DOF 场景不创建摄像头或手势推理组件，安装手部包不会自动启用相机。无需再逐个导入配套 tgz；它们作为校验和独立归档一并交付。不要另加第二个 Cardboard 包，本交付沿用上游版本号 1.30.0，但包含所需原生补丁，来源和哈希以清单为准。
源码目录通过 Add package from disk 导入时没有生成的内置归档，不触发自动安装；开发 SDK 源码时仍需自己配置本地配套包引用。

## 3. 配置 Android XR

1. 在 Build Settings 切换为 Android。
2. 执行 **Spatial AI > Setup > Configure Android XR**。该菜单配置 Cardboard loader、ARM64、IL2CPP、API 28/34、GLES3、横屏和 Input System。
3. 如果 Unity 提示因 Active Input Handling 改变需要重启，接受并等待重新打开。推荐 Both。
4. 在 Player Settings 设置自己的 Application Identifier。SDK 配置不会改动它，也不替换已有 Build Settings 场景。

SDK 构建回调自动加入固定版本 Android 依赖、AndroidX/Jetifier 和 Unity 2022 所需 R8 修复；不会调用仓库 tools 脚本，也不会使用开发者固定磁盘路径。

## 3.1 配置 UVC USB 设备声明

打开 **Edit > Project Settings > Spatial AI > UVC**：

- SDK 构建 Android 时自动声明可选的 USB Host 功能（`android.hardware.usb.host`、`required=false`），不要求设备必须具备 USB Host，也不会因此自动启动应用。
- **Associate On Attach** 默认关闭。关闭时 SDK 不注册 USB 插入启动入口；应用仍可在使用摄像头时请求授权。
- 开启后，在 **Devices (decimal VID / PID)** 中填写要匹配的设备。默认预填 HXQ：Vendor Id **4555**（0x11cb）、Product Id **10267**（0x281b）。字段使用十进制，可添加多个设备；重复条目在构建时合并。

启用关联时，SDK 自动为 Unity 主 Activity 添加 `USB_DEVICE_ATTACHED` intent-filter 和设备过滤 `meta-data`，生成 `res/xml/spatialai_uvc_devices.xml`。不需要手改 AndroidManifest.xml。开启时设备列表不能为空，VID/PID 范围为 0～65535；无效配置会中止构建并给出提示。已有其他插件管理同名 USB metadata 时会报告冲突，请先统一设备声明。

配置保存在用户项目的 `ProjectSettings/SpatialAIUvcSettings.asset`，建议纳入版本管理。配置属于构建设置，修改后须重新构建、安装 APK，运行时 C# API 不能改变已安装应用的 Manifest。关闭后再次构建时，也会清除复用 Gradle 导出目录中 SDK 自己生成的关联；用户自定义的 USB 声明会保留。

**设备关联不是永久静默授权开关。** 启用后 Android 可能显示应用选择提示；用户将应用设为默认处理应用后，插入匹配设备可能自动启动该应用。普通应用不能仅靠声明保证“拔插免授权且不自动启动”；定制设备如需此行为，需要系统 USB 授权策略支持。取消旧 Demo 的自动启动时，还应在设备系统应用设置中清除其默认打开关联，或安装移除该声明的新 Demo。

Demo 已移除强制关联 HXQ 的独立构建逻辑，统一使用此 SDK 设置；RhinoX 启动器分类仍由 Demo 自身保留。

## 4. 最简单的显示与 3DOF

先完成第 3 节的 Android XR 配置，再执行 **Spatial AI > Display > Create Display and 3DOF Scene**，将 `Assets/Scenes/SpatialDisplay.unity` 加入 Build Settings。
也可从基础包 Samples 导入 **Display and 3DOF**，打开导入的同名场景。
场景只创建 XR 相机、显示桥接和示例物体，标准 TrackedPoseDriver 读取 `<XRHMD>/centerEyeRotation`。
运行 Android APK 后应有双目显示，转头时固定场景物体相对视野移动；日志 `SpatialDisplay` 应显示 xr=True、rotationValid=True。
本机 Editor 无 Android Cardboard HMD 时，rotation unavailable 属正常状态。

## 5. 模拟手与真实手

在 Package Manager 中选择 **Spatial AI XR Hands**，Samples 分别导入 **Hands Simulation** 或 **Real UVC Hands**。
打开导入目录里的 `.unity` 文件；不会自动改动已有场景。也可通过 Spatial AI > XR Hands 菜单生成示例。

- 模拟手：Editor Play，点击 Game 窗口，用 1/2/3 切换张开/握拳/捏合，L/R 改变跟踪，空格切换运动。无需相机。
- 真实手：将 UVC 示例加入 Android Build Settings。连接 HXQ 并确认 USB 授权；没有相机时显示等待状态。
- 通过 `SubsystemManager` 获取运行中的 `XRHandSubsystem`，读取 leftHand/rightHand、GetJoint/TryGetPose；用 updatedHands 和 XRHandTrackingEvents 消费跟踪。缺失数据仍报告失效。

SDK 可视化统一采用身份稳定、侧掌最近距离投影和短暂漏检显示；这些保留骨架只用于显示，不能作为有效输入触发交互。
缺少标定时可以预览；设置 SpatialUvcHands 的 calibrationJson 可加载实际参数。不要把显示放大或反射补偿应用第二次。

## 6. 标准交互与手势

1. 先执行 **Spatial AI > XR Interaction > Import Unity XR Hands Gestures**，等待编译。这会原样导入 Unity XR Hands 1.4.3 的官方 Gestures 样例。
2. 在 Spatial AI XR Interaction 包的 Samples 中导入 **Hands Interaction**，打开导入的 `SpatialHandInteraction.unity`。包含两个方块、骨架、圆圈、普通 UI、标准手势 prefab 与 OPEN/FIST/PINCH 形状资产。
3. 或执行 **Create UVC Interaction Scene** 生成场景；模拟输入用 **Create Simulation Interaction Scene**。
4. 方块消费 XRSimpleInteractable 事件，普通按钮消费 Button.onClick。手势使用 XRHandShape/XRHandPose 与官方 StaticHandGesture.gesturePerformed/gestureEnded；跟踪使用 XRHandTrackingEvents。
5. 将场景放在 Build Settings 第一位，张开手使输入武装，用圆圈瞄准并捏合/松开；保持捏合不重复点击。丢失时取消，返回后先张开。

调试面板 AI/UVC/Render 分别为推理完成、采集回调、Unity 场景帧率。默认 CPU/VIDEO、固定 Single 单手模式；在 `SpatialHandTracking` 组件 Inspector 中设置 Inference Backend（CPU/GPU）和 Hand Mode（Single/Dual/Auto）。也可使用 `SpatialHandTracking.HandMode = SpatialHandMode.Single`（或 Dual / Auto）；启动前显式设置的 API 优先于 Inspector，配置为进程内全局设置。GPU 配置为 `SpatialHandTracking.InferenceBackend = SpatialHandInferenceBackend.GPU`，实际以日志 delegate 为准。XR Hands 标准接口没有 CPU/GPU 设置项。
原 Demo 的 am broadcast 是 Demo 专属调试入口，不会自动装入用户应用。

## 7. 构建、安装与排查

使用 Unity Build Settings > Build，或 **Spatial AI > Setup > Build Android APK**。后者读取当前启用场景，默认输出工程内 `Builds/Android/spatialai.apk`；可用 `SPATIALAI_APK_OUTPUT` 指定路径。
用 adb install -r 安装自己的 APK，启动自己的 Application Identifier。

| 现象 | 排查 |
| --- | --- |
| 没有 XR Hands 菜单 | 检查配套包已安装、依赖解析完成且 Console 无编译错误；基础包本身不提供这个菜单 |
| 安装入口没有完成 | 查找 Console 的 SpatialAIInstaller 错误；确认原始入口 tgz 可读取、网络可用、没有旧 Embedded 包覆盖；修正后执行 SDK Installation > Retry Installation |
| 无双目或 XR WAIT | 检查已安装配套 Cardboard、Android loader 启用、Android Build Target、GLES3 |
| 原生入口不存在 | 检查用了本交付的修改版 Cardboard，未被上游包替换，并核对校验值 |
| 等待 UVC / USB 重连 | 接回相机并确认现有 USB 授权，查看 SpatialUVC / SpatialHands；权限优化另列待办 |
| 没有手势事件 | 确认官方 Gestures 样例已导入且编译完成、prefab 引用有效；首先张开手恢复输入 |
| R8/Maven 或 API 错误 | 检查平台 34、Build Tools 30.0.3、External Tools SDK 和 Maven 网络 |
| 构建提示手部 AAR 缺失 | 使用完整发布包；源码检出需要由 SDK 开发者准备锁定制品 |
| 手部消失或未跟踪 | 查看 SpatialHandsComp 和几何拒绝信息；绘制保留骨架不等于有效标准跟踪 |

## 8. 升级

保存工程、提交或备份应用的 Assets、ProjectSettings 和 SDKPackages。通过 Package Manager 的 Add package from tarball 导入新版 `com.spatialai.sdk`，包内安装器自动更新三个配套包，旧归档和 manifest 备份继续保留；不要删除仍被工程引用的版本目录。无需额外导入独立 installer 或分别替换三个配套包。
不要删除已经编辑的示例或应用场景。SDK 设置和创建菜单不会重写已有同名场景；Package Manager 的重新导入 Samples 则按 Unity 的导入行为处理，可选择新的版本目录后自行合并应用改动。
升级后执行 Check Setup，检查官方 Gestures 的版本与 prefab 引用，重新构建，再验证显示、左右手、丢失恢复和 UI 点击。
本阶段提供对现有基础包 0.2.0-preview.9 的序列化场景保留检查；跨 Unity 大版本升级仍需单独验证。

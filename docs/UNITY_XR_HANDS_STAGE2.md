# Unity XR Hands 阶段 2：UVC 空间转换与标定工具

日期：2026-10-10。**软件与采集工具已实现；实机内参、安装外参和手部对位尚未验收。**
用户已确认 HXQ 摄像头固定在最终头戴位置，可以采集棋盘格。
根据后续决定，精度验收暂缓，缺少标定只提示、不阻止开发；标定工具和参数加载保留。
本文记录阶段 2 的交付；后续真实 UVC Provider、超时与恢复已在 [阶段 3](UNITY_XR_HANDS_STAGE3.md) 接入。
不得把本文合成检查或内参拟合通过写成真实手部精度通过。

## 本次交付

| 内容 | 入口 |
| --- | --- |
| 内参、畸变、镜像/旋转、安装变换及测量状态 | `UvcHandCalibration` |
| 21 点到 26 点、单目尺度、关节朝向、退化处理 | `MonocularHandConverter` |
| Android 旧识别帧转换为统一帧 | `SpatialUvcHandsConverter` |
| 手机预览图采集 | `Spatial AI > UVC > Create Calibration Capture Scene` |
| 现有示例 APK 的采集场景与远程保存 | broadcast 的 `scene=calibration` / `scene=capture` |
| 棋盘格、内参拟合、刚体外参拟合、误差分布 | `tools/CalibrateUvcCamera.py` |
| 未测配置与数据表模板 | [calibration](calibration/) |

原 Android AAR 与相机/推理实现未修改。基础 SDK 添加 Unity IMGUI 模块，用于采集按钮；不依赖 XR Hands。
标准关节映射与转换器复用同一份 21 点对应关系，避免两套索引漂移。

## 坐标、尺度与有效性

输入是 MediaPipe 图像归一化坐标。先去镜像，再撤销图像顺时针旋转，使用实际 HXQ
`fx/fy/cx/cy` 与 OpenCV 五参数 Brown-Conrady 畸变反投影。
相机空间约定为 Unity `+X` 右、`+Y` 上、`+Z` 前，单位米。相机畸变与镜片显示畸变是两组独立参数，
不复用 RhinoX 的八参数参考配置。

```text
原始 UVC 图像坐标 → 去图像变换/畸变 → 相机三维估算
  → headFromCamera → 物理头部 → originFromPhysicalHead → tracking origin
```

`originFromPhysicalHead` 必须由调用方提供物理头姿；禁止默认取已做镜片反射补偿的 `Camera.main`。
当前手机只有 3DoF，头部平移没有被跟踪，不能承诺房间尺度固定的手部位置。
手机倾斜约 45° 的安装数据也不能直接替代 HXQ 相机到物理头部的测量。

当前采用可审查的单目基线：用食指/小指 MCP（点 5/17）的实测宽度，以及腕点到中指 MCP（点 0/9）
的实测长度与射线张角估计深度；两条估计相差超过 1.6 倍拒绝该手，不把超出距离范围的估计夹到范围边缘。
关节深度再用腕相对图像 `z` 做 2.5D 近似。**这不是相机绝对测距，也不是已验证的抓取定位。**
MediaPipe 世界关键点描述局部手部结构，不能直接代替相机前的绝对位置；旧 AAR 目前没有提供世界关键点，
因此本次没有使用它，后续可与当前基线对照评估。

21 个对应点保留位置；Palm 为四个 MCP 的平均；四个缺失 Metacarpal 为腕与 MCP 的中点。
Palm/Root 的 `+Z` 为腕到中指 MCP，`+Y` 为按左右手确定的掌面法线；其余关节 `+Z` 沿指向末端的骨段。
所有位置和旋转都标记 `Estimated`，不提供有效半径或速度。
图像点越界、缺失、非有限值均无效；Palm 锚点缺失或法线退化时整手无效。
缺失指尖会同时使前一关节的旋转无效，但可保留该关节的位置。
这里没有逐点可见度输入，遮挡未被 MediaPipe 发现时仍可能产生错误估算，需要实测记录，不能用手势分类分数冒充关节置信度。

标定参数现在是可选项。`new SpatialUvcHandsConverter()` 或传入 `null`/空配置即可继续开发预览。
已有完整参数优先使用；部分缺少时仅补缺失字段，保留已填写的内参、畸变、安装变换和图像方向。
没有测量确认标记时也继续转换，不需要显式开启 `allowDevelopmentPreview`。
旧的双参数构造函数保留兼容，但标为 obsolete，布尔参数不再控制标定阻断。

默认候选值为：水平 FOV 60°（焦距由输入宽度推算、方形像素）、图像中心主点、零畸变、
相机到物理头部的零平移/单位旋转、掌宽 70 mm、腕到中指 MCP 80 mm、距离范围 0.2～1.2 m。
这些是通用预览假设，不是 HXQ 实测数据。传入的零占位焦距/掌尺寸、空数组等会补默认值；
错误相机 ID、错误分辨率、非有限数值、非单位旋转或不支持的畸变格式仍返回参数错误。

缺少、部分缺少或未确认测量时，`CalibrationWarning` 与逐帧 `diagnostics` 显示 `DEVELOPMENT`；
Unity 只在提示内容改变时写一次 `SpatialHands: Calibration ...` warning，避免每帧刷屏。
所有有效输出仍是 `MonocularEstimate`，位置和旋转保持 `Estimated`，不会把未测参数写回输入配置。
`UvcHandCalibration.Validate(false)` 保留用于严格测量状态评估，运行转换不再以此阻断。
`validatedForHardware` 独立保留最终验收状态，所有工具默认保持 `false`。
配置在创建转换器后应保持不变；换标定、归零或 tracking origin 时创建新转换器并增加 origin revision。

## 现在可以做：棋盘格内参采集

1. 打印 [9×6 内角点、20 mm 方格的棋盘格](calibration/hxq-board-9x6-20mm.svg)。
   外部是 10×7 个方格，整页含边框 240×180 mm，可用 A4 横向、100% 实际大小打印。
   贴在平整硬板上，量印刷后的实际格长；标定时传入实际测量值。
2. 保持最终相机安装、镜头焦距、640×480 模式；使用清楚且稳定的光照。
   继续手动曝光 16600 µs，曝光不足时先改善照明。棋盘格需静止、清晰，避免运动模糊和反光。
3. 在新 Unity 工程用菜单创建采集场景，将该场景加入 Android Build Settings 后构建。
   单独采集工程可关闭 XR 自动初始化；采集不需要标准手部 Provider。
   或构建本仓库维护的 SpatialXR 示例，新 APK 已包含采集场景和以下接口。
4. 启动 App、确认相机权限及 USB 权限后切换场景：

   ```powershell
   adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene calibration --ez gestures true --ei exposureMode 1 --ei exposureTimeUs 16600
   ```

5. 放好棋盘格、静止后点 Capture，或者电脑上发送：

   ```powershell
   adb shell am broadcast -a com.spatialai.samples.xr.SWITCH_SCENE -p com.spatialai.samples.xr --es scene capture
   adb logcat -s SpatialCalibration Unity
   ```

   `capture` 只触发保存，必须先进入采集场景。成功日志在 Unity tag 中包含 `SpatialCalibration:` 与路径。
   保持手势识别开启，用其结果确认相机 ID 和原始分辨率；拍棋盘格时不需要有手。
6. 采集 **25～30 张**：正面、左右/上下倾斜，板子覆盖中央、四角与边缘，改变距离和投影大小。
   所有内角点完整可见；每次移位后再静止拍摄。不要只拍同一个平面的连续帧。
7. 保存目录：`Application.persistentDataPath/uvc-calibration/<会话>/`。
   示例包通常为 `/sdcard/Android/data/com.spatialai.samples.xr/files/uvc-calibration/`：

   ```powershell
   adb pull /sdcard/Android/data/com.spatialai.samples.xr/files/uvc-calibration build/calibration/hxq-captures
   ```

   **交给后续处理时保留 PNG 和同名 JSON，整组文件一起保留。** 换包名时路径也要替换。

当前桥接预览是原始全视野 384×288 的 JPEG80，经 Unity 解码后保存 PNG，既不是原生 640×480 无损帧，
也不是镜片渲染结果。同名 JSON 记录原始模式/预览模式/镜像/旋转与序号；静态棋盘格不要求与最近识别结果同帧。
相同比例下可换算内参，报告同时记录原始误差与原生分辨率等效误差。
JPEG 与降采样会限制精度；若标定角点或后续误差不能达标，应增加原生帧保存路径，不能隐藏限制。
不接受缩放后视野改变的图像；更换 AAR 的裁剪/镜像/旋转处理后必须重新核对这份采集契约。

## 运行工具

需要 Python 3.12（本轮版本）及锁定的 OpenCV/NumPy。依赖与生成文件放在被忽略的 `build/`：

```powershell
python -m venv build/uvc-calibration-venv
build/uvc-calibration-venv/Scripts/python.exe -m pip install -r tools/uvc-calibration-requirements.txt
build/uvc-calibration-venv/Scripts/python.exe tools/CalibrateUvcCamera.py intrinsics --images build/calibration/hxq-captures/<实际会话目录> --runtime-size 640 480 --columns 9 --rows 6 --square-mm 20 --version hxq-20261010-intrinsics-v1 --output build/calibration/hxq-intrinsics-v1.json
```

`--images` 必须是直接放 PNG 的目录，`--square-mm` 必须是打印后的实际格长。
至少需要 18 张可识别图片；每五张保留一张作 held-out 检查，其余拟合。
重复图像不计数；拟合视角的板法线至少需要 10° 变化，否则质量检查不通过。
输出配置和 `.report.json`，列出拒绝图片、每图 RMS、输入 SHA-256、缩放和 held-out 误差。
默认 RMS 门槛是**采集分辨率** 0.7 px，用于拟合质量筛查，不是最终手部 30 mm 的验收门槛。
质量不通过时退出码 2，配置内 `intrinsicsCalibrated=false`；成功也只填内参，其他测量状态不会自动通过。

## 内参之后：安装外参、左右手和尺度

确定物理头部原点（如两眼中点）和坐标轴并记录测量方法。
外参是 `physicalHeadPoint = R * unityCameraPoint + t`，XYZ 都使用右/上/前、米，旋转为 xyzw 单位四元数。
如果来自 OpenCV 相机坐标，先用 `diag(1,-1,1)` 将相机点转成 Unity 坐标；不能直接复制 OpenCV 旋转矩阵。

工具提供刚体点集拟合：用独立夹具/测量方法取得至少 6 对覆盖空间体积、非共面的相机/头部坐标点，
填写 [外参模板](calibration/hxq-extrinsics-points.template.json) 的 `referenceSource` 和两组点。
不要用当前单目手部估算作为外参真值。图像内参采集本身不会提供相机到头部的关系。

```powershell
build/uvc-calibration-venv/Scripts/python.exe tools/CalibrateUvcCamera.py extrinsics --profile build/calibration/hxq-intrinsics-v1.json --points build/calibration/hxq-extrinsics-points.json --confirm-independent-reference --version hxq-20261010-install-v1 --output build/calibration/hxq-install-v1.json
```

拟合只允许 determinant=+1 的刚体旋转，输出点集 P95/RMS；默认 P95 >10 mm 拒绝写入结果配置。
这是测量工具的质量检查，不能替代独立对位点验证。如果没有可靠的独立空间参考，先保留 `extrinsicsMeasured=false`。

随后单独伸左手、右手验证原始标签与旋转/镜像，必要时设置 `swapHandedness`，记录证据后才设置 `handednessVerified=true`。
实测点 5→17 的掌宽、点 0→9 的长度，填入两个米制字段，再设置 `palmDimensionsMeasured=true`。
目前一份配置使用一组掌尺寸；不同用户/左右手尺寸差异会影响尺度，双手需分别检验。
这些参数不提供人群通用精度保证。

## 接口与后续精度测量

使用 `JsonUtility.FromJson<UvcHandCalibration>` 加载实际 JSON，创建 `SpatialUvcHandsConverter`，调用：

```csharp
var result = converter.Convert(rawFrame, physicalHeadInTrackingOrigin,
    receivedHostStopwatchNs, originRevision, out SpatialHandsFrame frame, out string diagnostics);
```

未提供 JSON/文件时直接 `new SpatialUvcHandsConverter()`；有参数时继续使用原有 JSON 加载代码，
再传入构造函数。缺少标定参数本身不会阻止此调用，提示可通过 `converter.CalibrationWarning` 显示到 UI。

`receivedHostStopwatchNs` 是调用方在接收时记录的主机 Stopwatch 纳秒值，不能传入旧 DTO 的 Android 时间戳。
旧源时钟未明确，统一帧的 source timestamp 为 -1 / Unknown；不能推导曝光至结果延迟。
配置或元数据错误时整帧拒绝；单手退化时该侧为 Untracked，另一侧仍可有效。
本阶段没有启动 UVC XR Provider。后续示例补充已将 `HandTrackingDemo` 的 UVC 分支接入此转换器：
使用 `Pose.identity` 明确以当前物理头部为局部原点，转换结果用于相机子节点上的独立 3D 骨架预览，
不读取补偿后的渲染相机姿态作为物理头姿，不承诺 tracking origin/世界空间追踪。
非 UVC 的 RhinoX 分支保留原投影；有测量 JSON 时通过示例的 `uvcCalibrationJson` TextAsset 加载。
阶段 3 才提供实际 XRHandSubsystem 消费场景和自动采样，当前可由测量脚本调用上述 API 导出位置。

按 [阶段 0 指标](UNITY_XR_HANDS_STAGE0.md) 在 0.3/0.5/0.7 m、多视野区域与掌朝向测量，
至少覆盖左右手腕和食指尖，各条件每组 ≥100 个样本；另做转头/抬头/侧倾及组合动作的动态对位。
使用 [CSV 模板](calibration/hxq-hand-accuracy.template.csv)，记录独立参考、估计位置、尺寸、朝向误差及有效标记。
无效采样也必须写行，不能丢掉失败帧。通过镜片的动态对位需另留人工观察记录，截图不能代替。

```powershell
build/uvc-calibration-venv/Scripts/python.exe tools/CalibrateUvcCamera.py accuracy --csv build/calibration/hxq-hand-accuracy.csv --data-kind hardware --evidence "夹具与距离测量记录路径" --output build/calibration/hxq-hand-accuracy.report.json
```

报告按 side/joint/condition 分组，P50/P95、有效率及样本数全部保留：位置 P95 ≤30 mm、
尺度 P95 ≤10%、有效角度 P95 ≤15°、有效率 ≥90%；样本不足不能通过。
未声明硬件数据时默认 synthetic，永远不能获得 hardware review eligible；工具也不会自动改配置最终验收标记。
未达到指标时先记录失败条件、收窄姿态范围或改深度方案，再决定接入真实交互。

## 本轮验证与未完成项

- .NET CoreChecks：976 项通过（阶段 2 新增 518 项），覆盖畸变/旋转/镜像、已知像素方向、21→26 点、
  双手和不同距离、安装与头姿组合、退化/遮挡/非法参数、估算能力标记。
- Unity 2022.3.14f1 独立基础 SDK 工程：545 项通过，包含以上转换检查、JsonUtility、左右手标签、
  重复/未知标签、时钟边界、采集场景序列化、无/部分标定继续预览及提示去重；无需 XR Hands 或 Cardboard。
  最新日志：`build/validation/xr-hands-stage2-fa0ae10cda294ec18e903c682f25cc91/verification.log`。
- Python/OpenCV 4.11.0、NumPy 2.2.6：4 个测试通过，真正检测 25 张合成棋盘图并拟合/保留验证视角；
  训练 RMS 0.123 px、held-out 最大 0.155 px。另验证外参、退化点集、误差/失败率及 synthetic 报告隔离。
- 2026-10-10 已重新构建并安装到小米手机 `10.168.2.104:5555`，安装与启动成功，APK v2 签名校验通过。
  APK：`build/samples/android/spatialxr-stage2-optional-calibration.apk`（53,522,459 字节）；
  构建日志：`build/logs/xr-hands-stage2-optional-calibration-apk.log`。
  SHA-256：`d6bd82c7e066804ef6665746f6db5b2b1328293de56359fa23628c9ea5fe62bc`。
- 已通过广播进入采集场景并保存一组 PNG/JSON：图像 384×288、原始模式 640×480、
  相机 `uvc:11cb:281b`、全视野、无旋转/镜像；证据：`build/validation/stage2-device-20261010/`。
  这是采集接口检查，不是棋盘格标定；手动曝光 16.6 ms 时采集约 30 fps，VIDEO/GPU 推理约 21～26 fps（无手画面）。
- 上述采集版 APK 的手势场景使用原有识别与显示链路，没有调用新的三维转换器或真实 UVC XR Hands Provider。
  无标定继续转换和提示已由上述软件测试覆盖，不能通过此 APK 验收真实 XR Hands 关节。
- 人工确认项：手势场景预览及单/双手识别、采集场景 Capture 按钮和 Saved 提示、
  `hands`/`calibration`/`imu` 场景反复切换及切回应用后恢复。
  内参/外参、单目深度误差、镜片动态对位暂缓；阶段 2 **未完成精度实机验收**。
- 阶段 1 回归再次通过 307 项、两轮 Play/Stop：
  `build/validation/xr-hands-stage1-aeb0e8c910f842829a440cb7d78e7686/verification.log`。
- 阶段 3 可以先使用上述默认参数接入真实 Provider；标定和精度验收后续补做，不作为当前开发的阻断条件。

### 同日补充：手机独立三维手预览

手机分支此前只创建 UVC 预览，没有启用 `HandTrackingDemo`；现已启用独立三维骨架，
UVC 分支消费上述转换器，移除手机手势场景的图像矩形（采集场景仍有预览）。
有 JSON 参数时优先使用，并将 `swapHandedness` 同步应用到骨架和手势文字；无参数提示并继续。
原始采集模式 640×480 @30，Android 手势模型和预览图均使用全视野 384×288。
这是当前物理头部为原点的估算预览，不是世界追踪或真实 XRHandSubsystem 接入。

- Unity 编辑器集成验证 104 项通过：双手、顺序变化、加载/缺失配置、左右手修正、退化、超时隐藏及启停。
  日志：`build/logs/uvc-3d-hand-demo-check.log`。
- 新 APK：`build/samples/android/spatialxr-uvc-3d-hands.apk`，53,524,071 字节，v2 签名校验通过，
  已安装到 `10.168.2.104:5555`；构建日志 `build/logs/uvc-3d-hands-apk-final.log`。
  SHA-256：`cbd381c7051fcc952fb3277e5d1b79a1716f4c9382985f20f6f2f06a5b93d0f1`。
- 人工验证：掌心面向相机、放在画面中央，单独伸左/右手，再双手张掌、握拳、捏合、离开视野。
  绿色 Left、青色 Right；无标定不验收位置与尺度。观察骨架是否出现、跟随和消失，以及文字类别/捏合提示。
  握拳/侧掌等姿态可能不满足单目几何条件而隐藏骨架；检测数量与几何有效性分别看 `SpatialHands` / `SpatialHands3D`。

用户随后确认镜片中手部只有左右反向、上下正常。当前小米 Host 新增 `reflectHandDisplayX=true`，
在骨架与捏合指针的最终展示层对 X 取反，Y/Z 保留；补偿针对当前安装、光路与输出方向的组合，
不从单次反射数量推断方向，也不修改相机内参/外参、输入镜像、手别或已调整好的视图姿态。
物理转换器的输出仍在反射前坐标。当前可视化只消费关节位置；后续若消费朝向，反射应按
`R_display = S * R_physical * S` 做基变换，不能把 determinant=-1 的 S 直接转成旋转四元数。
镜片修改后需重新验证该开关。新增显示补偿集成检查后 150 项通过：
`build/logs/uvc-3d-hand-mirror-check.log`；镜片中的实际方向仍待新版人工复测。
补偿版 APK：`build/samples/android/spatialxr-uvc-3d-hands-mirror.apk`，53,524,411 字节，v2 签名校验通过，
已安装到 `10.168.2.104:5555` 并切到手势场景；构建日志 `build/logs/uvc-3d-hands-mirror-apk.log`。
SHA-256：`aacdac916da3aeab59bf36de8f514cc642b34a691d7c27b90e2555e3ee9c502a`。

用户随后确认显示移动方向正确，但原始手别反向：真实左手显示 Right/青色，真实右手显示 Left/绿色。
当前小米/HXQ 的 Host 新增 `uvcSwapHandedness=true`，作为无标定预览的手别映射默认值；
原始 Right → 真实 Left，原始 Left → 真实 Right。转换器、骨架颜色、文字、手势与捏合使用同一映射。
`uvcCalibrationJson` 赋值后，其 `swapHandedness` 优先（包括显式 false）；此安装的实测配置应填 true。
该映射不修改原始识别帧，也不再次反射位置；显示层的 `reflectHandDisplayX=true` 继续保留。
其他 Host 默认不交换；阶段 3 真实 Provider 使用默认标定时也应继承当前 Host 的手别映射。
Unity 集成验证 161 项通过，包含双手不同手势、原始标签保留、显示方向不变和 JSON 优先级：
`build/logs/uvc-3d-hand-handedness-check.log`；新版真实左/右颜色和文字待用户复测。
手别修正版 APK：`build/samples/android/spatialxr-uvc-3d-hands-handedness.apk`，53,525,827 字节，v2 签名校验通过，
已安装到 `10.168.2.104:5555` 并进入手势场景；构建日志 `build/logs/uvc-3d-hands-handedness-apk.log`。
SHA-256：`fa9b4fd6d37f3bd908a8bedc3aa24b6cad81794e359622a9225cc04747e6f809`。

参考：[OpenCV 相机标定](https://docs.opencv.org/4.11.0/dc/dbb/tutorial_py_calibration.html)、
[MediaPipe Hand Landmarker 输出定义](https://ai.google.dev/edge/mediapipe/solutions/vision/hand_landmarker/python)。
上述算法是本 SDK 的单目估算基线，官方文档不保证这里的实际对位精度。

### 2026-10-10：翻手稳定与问题帧诊断

- 示例增加连续身份关联，保留原始标签；短暂几何失败仅在同一只手仍被检测时保留最后有效骨架最多 150 ms。
  严格 SDK 转换仍输出 Untracked，保留画面不能触发目标选择；持续拒绝、手消失或身份改变会隐藏骨架。
- Android 独立传递原始手别置信度，UI 显示 G/rawH 与 LOCK；SpatialHandsDiag 记录实际标签、两种分数、拒绝原因及 21 点分片快照。
- 核心 976 项、阶段 2 转换 545 项、Unity 示例集成 181 项检查通过。
  日志：build/logs/uvc-hand-core-regression.log、uvc-hand-stage2-regression.log、uvc-hand-stability-check-final.log。
- build/samples/android/spatialxr-uvc-hand-diagnostics.apk 构建成功，53,532,371 字节，v2 签名通过。
  SHA-256：430814256f91ed7e59e28ec2d5518a0f67b334115920b92167e899b2fa1ae296。
  已安装到 10.168.2.104:5555 并启动；构建日志 build/logs/uvc-hand-diagnostics-apk.log。
- 实际侧掌稳定程度和持续几何拒绝原因待用户复现日志确认；抓取方法见 [翻手诊断文档](UVC_HAND_DIAGNOSTICS.md)。

### 同日补充：依据翻手日志改善侧掌和漏检显示

实机日志 uvc-hand-turn.log 显示 Palm axes disagree、原始手别跳变和 not detected；因此增加展示退化。
同一身份侧掌造成掌宽/掌长深度冲突时，沿用最近有效相机距离，并投影当前 21 点，显示 depth-held。
距离仅在正常几何恢复时更新；不能跨身份继承，关键点不完整/越界或其他拒绝原因不进入该路径。
短暂漏检保留最近画面最多 120 ms，显示 missing-held，按渲染帧到期隐藏。
严格空间转换仍是 Untracked；退化画面不悬停或选择目标。先以正常掌姿获得距离，再翻手验收。
快照分片改为 600 字符，避免实机 1400 字符分片被截断。日志增加左右相机距离与漏检保留到期事件。
Unity 集成检查 188 项通过：build/logs/uvc-side-palm-check-final.log，包含持续侧掌、关键点跟随、
身份重建禁止复用旧距离、漏检到期无新帧也隐藏及全部快照分片重建 JSON。
操作与限制见 [翻手诊断文档](UVC_HAND_DIAGNOSTICS.md)。

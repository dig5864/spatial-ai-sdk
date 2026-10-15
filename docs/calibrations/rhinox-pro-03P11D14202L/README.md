# RhinoX Pro 03P11D14202L：RGB 内参与旧服务外参来源

日期：2026-10-15。设备 `sys.ximmerse.model.version=150`，按服务代码走 RhinoX Pro 分支。
用户提供的 XML 与当前设备 `/backup/rgb_vio_camera_params.xml` SHA-256 完全一致：`76cb0a71abae40cd3dd19a394eb47da79571d8d8b4e301e72dfe1019501d1331`。

## 生成结果

- `rgb-intrinsics.json`：独立 RhinoX RGB 内参，1440×1080，fx=708.136843422096、fy=706.8877379361797、cx=708.9547400111156、cy=550.0180787227546。采用 RGBDistCoeff 的8项有理模型；保留原始14项供追溯，末6项均为0。字段可由 SpatialRgbCalibration 解析，当前未修改 LoadRhinoXReference 的资源选择，未部署新的 APK。
- `rgb_vio_camera_params.xml`、`device_calibration.xml`：从本台设备只读导出的匹配原始文件，保留注释中的矩阵。deviceUID 写为 not_configured，设备归属由本次ADB序列号和文件哈希记录，不能凭该字段自动匹配硬件。
- `paramloader-design.json`：在设备上调用服务源码随附 libsxrapi.so 的只读 ParamLoader 接口取得的 Eye→IMU 设计值；记录库 SHA-256。它是对应库提供的设计配置，不等同于本次重新测量的佩戴眼位或支架外参。
- `extrinsics-from-service.json`：复现当前 C++ 服务实际启用分支的数值矩阵及 helper 输入翻转。只是服务链路参考，不是可直接应用于当前 UVC 的 headFromCamera 配置；坐标基、头部原点及物理对位尚未验证。
- `verification.json`：文件一致性、14→8项畸变等价与HXQ参数未变的校验结果。

## 外参到底从哪里读取

源文件：`D:/code/android/xruntime/handdataservice/src/CameraParamLoader.cpp`。

| 输入 | 读取入口 | 用途 |
| --- | --- | --- |
| `/backup/aio/rgb_vio_camera_params.xml` | 第49行定义、第134行检查 | 优先 RGB/VIO 配置，本台不存在 |
| `/backup/rgb_vio_camera_params.xml` | 第50行定义、第140行回退读取 | 本台实际使用；RGB内参、RGB与右VIO之间的旋转/位移 |
| `/backup/slam/device_calibration.xml` | 第51行定义、第114行读取 | 右/左VIO之间变换，以及左VIO与IMU关系 |
| `ParamLoaderGetFloatArray(Design_Eye2IMU_TransMat_OpenGL_ARRAY16)` | 第91–101行 | 从 libsxrapi.so 参数系统取眼中心与IMU设计变换，不是RGB XML中的字段 |

本台 `/sdcard/vio_fusion_configs.json` 未发现。HMD20 的 xdevc_get_camera_calibration 分支不是本台路径；同仓头文件还注明该API在RhinoX Pro不支持。

## 实际外参使用链

1. `CameraParamLoader.cpp:167` 计算源代码命名的 `T_IMU2VIOL * inverse(T_VIOR2VIOL) * T_VIOR2RGB`。
2. 第183–193行重新排列平移轴并旋转坐标基；第195行左乘 `inverse(T_Eye2IMU)`；第196/198行继续翻转平移X/Z；最终写入 TransMat4_RGB。
3. `helper.cpp:60–72` 在对每个3D点应用矩阵前先把原始点变为 `(x,-y,-z)`；第168/231行调用这段转换，再输出手势结果。touchless_a3d/event.h:73–77说明输入相机坐标点单位米。
4. `helper.cpp:386–395` 把外参交给引擎的代码位于 #if 0，当前不启用；当前引擎使用内参，3D结果之后才经 TransformPoints 转换。
5. `CameraParamLoader.cpp:152–159` 的简单乘积也是 #if 0，不能把它当作当前实际输出。

额外发现：`CameraCalibrationParam.cpp:46–102` 读取 device_calibration.xml 注释中的 `Ombc/Tbc as (R | t) matrix:` 三行，填入 sfConfig.TransMat；没有直接使用结构化 Stateinit 的 ombc 重算旋转。导出时必须保留XML注释，否则旧解析器可能得不到矩阵。

## 旧服务中的参数混用问题

`CameraParamLoader.cpp:206` 将 `mRGBCal.VIODistCoeff[i]` 赋给 `Distortion_RGB[i]`，而第210行使用 RGB 的 CamMat。`CameraCalibrationParam.cpp:610–618` 明确分别读取 RGBDistCoeff 和 VIODistCoeff，因此此赋值是可见的不一致，生成内参时没有照搬；本次也没有修改服务源码。

旧Unity客户端还包含自身的坐标转换和经验补偿，不能仅凭变量名把旧服务矩阵直接复制到当前 Unity UVC 的 physical-head 坐标系。当前HXQ保持此前棋盘格内参、实测掌尺寸与1倍展示；没有用RhinoX内参覆盖HXQ，也没有将旧服务外参自动应用于支架。

## 验证与继续工作

使用 OpenCV 4.11.0 对5个不同方向的三维点比较源14项模型与导出8项模型，投影最大差值为0px；JSON保存原始双精度参数。服务链重算的旋转 determinant约1.000000338，微小偏差来自设备文件打印精度，未擅自归一化或把它标记成硬件验收通过。

HXQ标定配置前后SHA-256均为 `73c02ad5deb7a76b04cf07678a2cc4c747ab1c27f114509388703de434d4da20`。
已取得复现服务链所需的两个设备XML和设计Eye→IMU矩阵。后续可基于此进行RGB相机→Unity物理眼中心的坐标适配与方向/距离验证，再决定是否作为安装几何相同支架的外参候选。

本地复现脚本：`build/calibration/generate_rhinox_profiles.py`；只读API探针源码与原始输出：`build/calibration/rhinox-pro-03P11D14202L/`（build文件被Git忽略）。
### 2026-10-15 RhinoX Pro 本机 XRI 验证包

- 设备 `03P11D14202L` 使用 Camera2 RGB `2`、1440×1080，以及本目录 `rgb-intrinsics.json` 的完整8项有理畸变。HXQ的640×480内参保留在原资产。
- 原生RGB的手部配置：`samples/unity/SpatialXR/Assets/Resources/SpatialAI/Calibrations/rhinox-pro-03P11D14202L-hands.json`。使用此前转换的初始 camera-to-head 位姿，掌尺寸64×90 mm，展示倍率1。
- Demo在RhinoX/kona上默认选HostProfile，从而进入rhinox-rgb路径；手机仍默认UVC。原生帧必须与显式标定的cameraId和图像尺寸匹配，错误相机或尺寸不进入转换。
- 几何模型兼容5项Brown和8项rational，未截断RhinoX畸变系数。`RhinoXHandCalibrationCheck` 验证25个采样射线、合成手骨架的外参应用，以及错误相机/尺寸的拒绝。
- 本机安装/左右手映射仍待佩戴实测，`extrinsicsMeasured`、`handednessVerified`、`validatedForHardware`保持false。旧的通用builtin-front/rear固定深度预览不受本入口影响。
- 已安装到 `03P11D14202L` 并进入 `SpatialHandInteraction`。日志确认本机配置版本 `rhinox-pro-03P11D14202L-rgb-hands-initial-v1` 已加载，相机约30fps、推理约28fps、画面渲染约72fps，管线错误0。核对时镜头前未检测到手，真实手骨架的大小/位置/双眼合像仍待用户佩戴验证。
- APK：`build/samples/android/spatialxr-rhinox-pro-initial-calibration.apk`；SHA256：`487ea93d4cbcc1b88db96af24afca9cf77160947cdf3d3b958dbe9abfb74b653`。部署记录见 `xri-deployment-verification.json`。

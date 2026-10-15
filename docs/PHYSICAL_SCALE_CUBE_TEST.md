# 3DoF：20 cm Cube 直尺测试

场景：`samples/unity/SpatialXR/Assets/Scenes/PhysicalScaleValidation.unity`。
独立 APK：`build/samples/android/spatialxr-physical-scale.apk`，包名 `com.spatialai.validation.scale`。

## 操作

1. 安装 APK，打开 **Physical Scale Test**。复用已导出的固定支架配置，仅适用于相同镜片、屏幕安装平面和倾角。
2. 首次使用本测试应用时，点击右上角 **齿轮** 打开屏幕尺寸校正，核对双轴标尺，选择 **Save measured screen** 并关闭。其他应用保存的屏幕尺寸不会自动进入本应用；不要在新手机重新建立支架。
3. 戴好设备、固定眼位并正视前方，点屏幕将 Cube 摆到初始视线下方约 **15°**，方便伸手拿尺子对比。目标随后保持场景坐标，不随转头移动；点按仅重新摆放目标，不改变光学参数。
4. 伸手将真实直尺横向拿在眼睛前方约 **50 cm**、视线下方的位置，与 Cube 正面大致平行。距离从眼睛量起，不从镜片或手机量起；沿眼睛到 Cube 正面中心的斜线测量。50 cm 处的正面中心比初始眼位低约 13 cm。先做舒适的手持粗测，需要精确误差时再固定直尺并量准距离。
5. 遮住右眼，用左眼将 Cube 正面左边缘与尺子某一刻度对齐，读出右边缘跨度；固定眼位后换右眼复测。也可竖放尺子测高度。
6. 记录实际跨度、尺子距离、眼位/装配、左右眼差异及是否重影。重新佩戴后重新归零，再进行重复测量。

Cube 边长 **0.20 m**，正面中心距初始眼位 **0.50 m**，Cube 中心距初始眼位 **0.60 m**。目标位于初始视线下方 **15°**，正面朝向观察者，横边保持水平。中心十字位于正面内，不扩展量尺边界。量的是 Cube 正面两侧边界，不量偏转后整体轮廓，也不量十字或说明文字。

本测试验证固定眼位、视野中心、单一距离下的尺度；没有位置追踪，不验收前后左右移动头部后的现实对齐。场景沿用 RhinoX 参考光学参数，保持 `opticsValidated=false`。

| 眼睛 | 正面距离 | 对应实际宽 | 对应实际高 | 备注 |
| --- | --- | --- | --- | --- |
| 左眼 | 约 500 mm | 待测 | 待测 | |
| 右眼 | 约 500 mm | 待测 | 待测 | |

尺度误差 = `(实际读数 mm - 200) / 200 × 100%`。正式允许误差尚未约定，本轮记录原始读数，不自动宣称光学标定通过。

## 构建

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/BuildPhysicalScaleValidation.ps1
```

脚本在隔离项目中生成场景并构建，不修改完整 Demo 的默认启动场景。也可在 Unity 示例工程选择 **Spatial AI/Create Physical Scale Validation Scene**，将场景加入 Build Settings 运行。

生成时检查实际 Cube 网格尺寸为 0.2×0.2×0.2 m，正面中心距眼位 0.5 m、Cube 中心距眼位 0.6 m、下移角度 15°，目标根节点不挂在相机下。启动/重新摆放后输出 `SpatialAI SCALE` 日志，记录尺寸、距离、下移角度、位置和屏幕布局状态。最终光学结果需佩戴实测。

## 2026-10-13 构建与启动验证（初版 1 m）

- Unity 2022.3.14f1 Android ARM64 IL2CPP 构建、实际 Cube 几何检查和 APK v2 签名检查通过。
- APK SHA-256：`45e0502361bb9d4f12235c256f978eed6a2dedd209d88f24845247e800ed3863`。
- 已安装并启动于 Xiaomi 2206123SC / mayfly（ADB `7ca8ea63`）。修复初次摆放早于显示/姿态配置生效的问题；当前等待原生布局 applied 后至少 1 秒，再捕获相机姿态摆放目标。
- 运行日志确认 revision=1 已应用，随后输出 placement=1、frontPlane=1.000m、center=1.100m；截图确认双眼均有绿色 Cube 和说明文字。
- 本独立应用当前为 `ESTIMATED layout; reference optics UNVERIFIED`，首次使用仍需在该应用内核对标尺并保存手机尺寸。裸屏截图中反畸变后的形状、方向不作为镜片后的光学通过证据。
- 构建日志：`build/validation/phone-screen-unity/physical-scale-build.log`。设备日志与截图：`build/validation/physical-scale-mayfly/startup.log`、`startup.png`。实际直尺测量尚待用户完成。

### 同日：校正面板可读性调整

根据用户反馈，右侧裸屏校正面板采用 28–32 号文字、更大输入框及按钮，按输出高度适配；仅缩放控件面板，双轴 50 mm 标尺仍按实际输出像素和有效屏幕尺寸计算。Save measured screen 保存后仍需点 Close / resume display 返回 Cube 场景。Cube 场景说明文字同时放大两倍、缩短每行内容并上移，Cube 的尺寸与距离保持不变。

更新 APK SHA-256：`a12a0d84f44d6aae18d780ce632b6b9aee877564b92c85307a4be577ee5c230f`。Unity 构建和实际 Cube 几何检查通过；已通过 Wi-Fi ADB `10.168.2.104:5555` 更新并启动小米 12。截图 `build/validation/physical-scale-mayfly/panel-large.png` 确认面板完整可见、文字和控件增大、左右标尺完整保留。此次仅检查可读性与布局，不增加光学验收结论。

### 同日：手持直尺 50 cm、目标下移

按用户伸直手拿尺子约距眼睛 50 cm 的使用条件，将 Cube 正面中心距离改为 0.5 m、Cube 中心距离改为 0.6 m，目标放在初始视线下方 15°，正面中心约低 13 cm。正面朝向观察者，20 cm 边长不变。点屏幕将目标重新摆到当前视线下方，目标随后保持场景坐标。说明缩短为四行并下移，避免标题被 AA 区边缘裁切。

最新 APK SHA-256：`7e47d760593b6c4d8cee580cd80c2ae2d3af5c8eb5f6aaac1035b9938e53a274`。Unity ARM64 IL2CPP 构建、网格尺寸/前表面距离/中心距离/下移角度检查及 APK v2 签名检查通过；已通过 Wi-Fi ADB 安装并启动。运行日志确认 `frontPlane=0.500m center=0.600m depression=15deg`，布局为 `MEASURED`，原生 revision 已应用。截图确认双眼目标位于下方、说明完整显示。日志及截图：`build/validation/physical-scale-mayfly/half-metre.log`、`half-metre.png`。用户的手持直尺读数仍待记录，完整光学状态仍为 UNVERIFIED。

### 同日：用户报告无法双眼合相

用户确认裸屏水平、竖直两条线实测均为 50 mm，但 Cube 中心十字始终无法对齐；进一步确认左右、上下均有错位。记录为本装配、50 cm 下方目标的双眼合相未通过，不据此认定具体参数或左右眼交换，也不修改光学验证标记。

运行日志 `build/validation/physical-scale-mayfly/fusion-check.log` 确认 MEASURED 布局与原生 revision 已生效，两眼 AA 为原参考矩形。当前光学仍采用未验收 RhinoX 网格、IPD 62 mm、双眼共用参考视锥，尚无此支架的独立左右眼光学中心/投影标定。裸屏标尺只验证屏幕物理尺寸换算，不能证明镜片后双眼对齐。下一步先排查佩戴与每眼中心，再通过远近目标比较区分固定对齐偏差与深度视差；在合相问题解决前，尺度可分别单眼记录，不能宣称完整双眼验收通过。

### 同日：安装到 USB RhinoX 做对照

按用户要求，将同一份 50 cm 测试 APK（SHA-256 `7e47d760593b6c4d8cee580cd80c2ae2d3af5c8eb5f6aaac1035b9938e53a274`）安装到 USB 设备 `03P11D14202L`（QUALCOMM / RhinoX / kona），安装成功并启动。日志确认选择 RhinoX 参考光学及 IMU 设计参数，输出 `frontPlane=0.500m center=0.600m depression=15deg`。该设备走 RhinoX 显示路径，不使用小米手机支架 AA 配置；`PhoneDisplayStatus=Not configured` 在此表示未走手机布局路径。光学仍为未验收参考，佩戴合相及尺度结果待用户对照反馈。运行日志位于 `build/validation/physical-scale-rhinox/startup.log`。

### 同日：RhinoX 用户对照结果

用户确认该设备上的测试能够双眼合相，手持直尺简单测量 Cube 高度和宽度均接近 20 cm，估计误差为 1–2 cm。记录为本次佩戴、50 cm 下方目标的主观合相通过，单点尺度粗测接近预期；用户未提供左右眼独立读数、误差正负及精确直尺距离。相对 20 cm，1–2 cm 对应约 5–10% 的读数偏差，包含手持直尺距离与角度等测量不确定性，不能直接认定为光学尺度误差。

同一 APK 在 RhinoX 可合相而在小米反射支架上无法合相，支持优先排查手机反射支架的每眼光学中心、左右眼投影/光路映射及佩戴装配，而不是优先改 Cube 尺寸。两设备显示和追踪配置不同，此对照不能单独确定具体错误参数，也不能将 RhinoX 结果转为小米通过。继续保留光学验证标记为 false，后续正式验收需约定容差并复测多距离/位置。

### 同日：第二台小米 dada 对照准备

按用户提供的 Wi-Fi ADB 地址 `10.168.2.141:45921` 连接 Xiaomi 24129PN74C / dada，安装同一份最新 50 cm 测试 APK 并启动。运行日志确认固定支架后备配置、2670×1200 输出、原生 applied revision=1 以及 `frontPlane=0.500m center=0.600m depression=15deg`。初次运行尺寸为 ESTIMATED，需在本测试应用内核对双轴 50 mm 标尺并 Save measured screen，再 Close / resume display；没有重新建立参考支架。合相和尺度结果待用户反馈。启动日志与截图：`build/validation/physical-scale-dada/startup.log`、`startup.png`。

### 同日：校正入口移至右上角齿轮

用户反馈顶部 Screen size 大按钮容易误触，改为点击现有 Cardboard 右上角齿轮。移除顶部中央的可见按钮，Android 透明原生点击区域按 `Widget.GearButtonRect` 定位，转换输出像素/窗口尺寸及坐标原点；XR 停止时提供同位置的齿轮作为校正恢复入口。校正打开时隐藏入口，关闭后恢复，不改变标尺或光学参数。

最新 APK SHA-256：`22e7a2a94777e9866c4ec6bc8f3cfa3b89af1f69eeb71d5bb57e05b6a6552c16`。Unity 构建、Cube 几何及 APK v2 签名检查通过，已安装到 dada。截图确认顶部中央不再有按钮；原生 UI 树确认齿轮点击区域位于 `[2534,0][2670,136]`，可点击且启用。该设备限制 ADB 输入注入，用户手动确认齿轮可以打开校正页，Close / resume display 可以返回 Cube。证据位于 `build/validation/physical-scale-dada/gear-scene.png`、`gear-ui.xml`、`gear.log`。

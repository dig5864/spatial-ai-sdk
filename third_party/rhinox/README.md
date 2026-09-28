# RhinoX 畸变适配（诊断版）

源数据来自历史工程 `SXR-SDK-405/sxrApi/sxrApi/private/SxrDistortionManager/DistortionMesh_NEDPLUS_2k_221108.cpp`。历史运行时代码设置 `gMeshVersion=221108`，`DistortionMesh_Sharp_2k_Seperate.h` 因此选择这组数据；同目录 `DistortionMesh_Sharp_2k_Seperate.cpp` 内的 `gXimmerseAR` 网格只在回退分支使用。

仓库中保留原始双眼数组 `spatial_rhinox_mesh_data.cc`，SHA-256 为 `ff6755f07b5657310a63165f7ba81689a3ce97c28892031b1ed5d89415359d74`。每眼 6069 个顶点、11800 个三角形。`cardboard-display-api.patch` 只修改 Cardboard v1.30.1 的 Unity 原生显示适配：将顶点用于全屏裁剪坐标；历史 UV 的 X 轴沿屏幕纵向、Y 轴沿屏幕横向，且两眼方向相反。左眼转换为 Cardboard 的 `u=(legacyV+1)/2`、`v=(1-legacyU)/2`，右眼为 `u=(1-legacyV)/2`、`v=(legacyU+1)/2`；再用退化三角形把独立三角形转换成 Cardboard 使用的 strip。投影参数取自 `Param_RhinoXPro.cpp`：IPD 62 mm，左右半宽 0.07968503 m，上高 0.07348122 m、下高 0.08588884 m，近裁面 0.1 m。

`tools/BuildRhinoXCardboard.ps1` 固定 Cardboard 源码提交 `67bee10850562bf8cbe563a22b47eca8ffd8f1eb`，编译 Android arm64 后替换 Cardboard XR Plugin v1.30.1 AAR 的相应原生库。Unity 工程依赖的插件目录位于被忽略的 `third_party/checkouts/cardboard-xr-plugin-unity2022`；在新机器上运行构建脚本会拉取固定标签并生成目录。`third_party/rhinox/GfxPluginCardboard-rhinox.aar` 是生成的可追踪产物，便于远端仓库检出后核对版本。

启用条件为已知 RhinoX 设备身份、Unity 场景显式选择和 4320×2160 屏幕参数。设备内显示 `applied` 表示网格已提交给 Cardboard 渲染器。该历史数据尚未完成在当前 SDK、设备固件与佩戴者视场下的视觉校准；正式 profile 的 `validatedForHardware` 保持 `false`。iOS 暂不使用此 Android 网格。

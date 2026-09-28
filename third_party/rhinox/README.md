# RhinoX Pro 畸变适配（诊断版）

源数据来自历史工程 `SXR-SDK-405/sxrApi/sxrApi/private/SxrDistortionManager/DistortionMesh_Sharp_2k_Seperate.cpp` 的 `gXimmerseAR_WarpMesh` 双眼数组。`svrApiCore.cpp` 中 `Device_RhinoXPro` 选择 `kDistortionMesh_Sharp_2k_Seperate`；`gMeshVersion` 默认为 `-1`，因而走该类的默认数组。`221108` 数据由 `Device_HMD20` 分支显式选择，不是 RhinoX Pro 的默认网格。实际设备若另有配置覆盖 `gMeshVersion`，还需从运行日志核实。

仓库中的 `spatial_rhinox_mesh_data.cc` 是上述历史源文件的原样副本，SHA-256 为 `9ce72f86ada58cf53ab0d440fd504c1197987918be09770f0852faea5512d969`。每眼 2601 个顶点、5000 个三角形。历史网格的坐标覆盖完整双眼屏幕；UV 各轴在 `[-1,1]`，U 从左到右、V 从上到下。`cardboard-display-api.patch` 将其转换成 Cardboard 的 `u=(legacyU+1)/2`、`v=(1-legacyV)/2`，再用退化三角形把独立三角形转换成 Cardboard 使用的 strip。投影参数取自 RhinoX Pro 历史参数：IPD 62 mm，左右半宽 0.07968503 m，上高 0.07348122 m、下高 0.08588884 m，近裁面 0.1 m。

`tools/BuildRhinoXCardboard.ps1` 固定 Cardboard 源码提交 `67bee10850562bf8cbe563a22b47eca8ffd8f1eb`，编译 Android arm64 后替换 Cardboard XR Plugin v1.30.1 AAR 的相应原生库。Unity 工程依赖的插件目录位于被忽略的 `third_party/checkouts/cardboard-xr-plugin-unity2022`；在新机器上运行构建脚本会拉取固定标签并生成目录。`GfxPluginCardboard-rhinox.aar` 是仓库内的生成产物。

参考 APK 的 4320×2160 原始截图中，双眼中央白色竖线分别位于约 x=1060 和 x=3261。该网格中心顶点的 x 坐标为 -0.509533 和 +0.509533，映射到整屏后约为 x=1059 和 x=3261；中心 y=-0.185164 映射后为 y=1280。位置吻合，支持这组数据属于参考画面。参考截图里的文字相对源图呈上下颠倒状态；这可能涉及历史 Runtime 的纹理方向或屏幕安装方向，仍须取得 SpatialXR 原始截图后才能确定 Cardboard 侧是否需要翻转 V。

启用条件为已知 RhinoX 设备身份、Unity 场景显式选择和 4320×2160 屏幕参数。设备内显示 `applied` 表示网格已提交给 Cardboard 渲染器。还需用同一张测试图对比历史 Runtime 与 SpatialXR 的真机截图，确认眼别、方向、位置、局部网格形变和边缘裁剪；在这之前正式 profile 的 `validatedForHardware` 保持 `false`。iOS 暂不使用此 Android 网格。

# RhinoX Pro 畸变适配（诊断版）

源数据来自历史工程 `SXR-SDK-405/sxrApi/sxrApi/private/SxrDistortionManager/DistortionMesh_Sharp_2k_Seperate.cpp` 的 `gXimmerseAR_WarpMesh` 双眼数组。`svrApiCore.cpp` 中 `Device_RhinoXPro` 选择 `kDistortionMesh_Sharp_2k_Seperate`；`gMeshVersion` 默认为 `-1`，因而走该类的默认数组。`221108` 数据由 `Device_HMD20` 分支显式选择，不是 RhinoX Pro 的默认网格。实际设备若另有配置覆盖 `gMeshVersion`，还需从运行日志核实。

仓库中的 `spatial_rhinox_mesh_data.cc` 是上述历史源文件的原样副本，SHA-256 为 `9ce72f86ada58cf53ab0d440fd504c1197987918be09770f0852faea5512d969`。每眼 2601 个顶点、5000 个三角形。历史网格的坐标覆盖完整双眼屏幕；UV 各轴在 `[-1,1]`，U 从左到右、V 从上到下。历史着色器将两个 UV 分量都按 `(value+1)/2` 转换；`cardboard-display-api.patch` 保持这一方向，再用退化三角形把独立三角形转换成 Cardboard 使用的 strip。投影参数取自 RhinoX Pro 历史参数：IPD 62 mm，左右半宽 0.07968503 m，上高 0.07348122 m、下高 0.08588884 m，近裁面 0.1 m。

`tools/BuildRhinoXCardboard.ps1` 固定 Cardboard 源码提交 `67bee10850562bf8cbe563a22b47eca8ffd8f1eb`，编译 Android arm64 后替换 Cardboard XR Plugin v1.30.1 AAR 的相应原生库。Unity 工程依赖的插件目录位于被忽略的 `third_party/checkouts/cardboard-xr-plugin-unity2022`；在新机器上运行构建脚本会拉取固定标签并生成目录。`GfxPluginCardboard-rhinox.aar` 是仓库内的生成产物。

参考 APK 的 4320×2160 原始截图中，双眼中央白色竖线分别位于约 x=1060 和 x=3261。该网格中心顶点的 x 坐标为 -0.509533 和 +0.509533，映射到整屏后约为 x=1059 和 x=3261；中心 y=-0.185164 映射后为 y=1280。位置吻合，支持这组数据属于参考画面。参考截图里的文字相对源图呈上下颠倒状态，也与历史着色器直接使用 V 分量的方向相符；仍须取得 SpatialXR 原始截图确认新适配的实际输出。

启用条件为已知 RhinoX 设备身份、完整的光学 Profile、与原生网格一致的 ID 和 SHA-256，以及 4320×2160 屏幕参数。Unity 自动传入 IPD 和视锥；原生层直接使用 RhinoX 网格与投影，不读取通用 Cardboard QR 镜片参数。设备内显示 `applied` 表示网格已提交给 Cardboard 渲染器。历史参考图与 SpatialXR 真机截图的几何位置已接近一致，但每台设备的光学校准仍未完成，因此 `validatedForHardware` 保持 `false`。iOS 暂不使用此 Android 网格。

Reference display aspect: native RenderEyesToDisplay fits the RhinoX two-square-eye mesh into a centered 2:1 viewport using the actual available viewport (including cutout exclusions). This is the fallback when no explicit per-eye rectangles are supplied. Mesh UVs and distortion coefficients are unchanged. This is a reference visualization, not phone lens calibration.

Xiaomi 2206123SC reference layout now uses user-supplied Android top-left full-panel pixel rectangles: left (268.220,157.045,865.054,865.054), right (1266.727,157.045,865.054,865.054), on 2400x1080. For each original half-screen mesh vertex, u = x_ndc + 1 (left) or x_ndc (right), v_top = (1-y_ndc)/2; target X = rect.x + u*rect.width, Y = rect.y + v_top*rect.height; final NDC = (2X/2400-1, 1-2Y/1080). Original texture UVs remain unchanged. This avoids safe-area offsets and preserves user-provided physical-density scaling. The rectangle centre is not necessarily the optical centre of the asymmetric RhinoX mesh.

# RhinoX Pro 调试适配

初期调试目标，尚未接硬件。

参考现有 `SDK/XMS_NativeSDK/library/sdk/unity/xr_provider` 的显示、输入与生命周期；从 `SDK/SXR-SDK-405` 提取型号匹配的光学参数和网格。确认固件 API、设备姿态来源、显示模式和坐标后，替换 `PendingHardwareBackend`。

不要把旧一体机的私有系统路径、SVR/VPU 库和量产参数复制为所有手机默认值。每次样机验证记录固件、profile 与原生依赖。RhinoX Pro 的运行结果与手机外接眼镜链路分开记录。

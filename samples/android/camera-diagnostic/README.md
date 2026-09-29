# CameraService Android 诊断样例

本样例只验证 Android Camera2 相机发现、采集、帧缓冲生命周期和双消费者分发。它不会修改 Unity 场景，也未接入 MediaPipe 或 Vuforia。USB 摄像头若未由设备 Camera HAL 暴露给 Camera2，当前样例不会发现它，后续需增加独立 UVC 采集适配器。启动时优先选择报告为彩色传感器的相机；彩色预览由采集的 I420 帧转换，单色传感器保持灰度预览。

## 构建与安装

```powershell
cd E:\wuyc\visor-x\SpatialAISDK
.\tools\BuildCameraDiagnostic.ps1 -AndroidPlayerPath 'D:\Unity Hub\2022.3.8f1c1\Editor\Data\PlaybackEngines\AndroidPlayer'
adb install -r .\build\samples\android\camera-diagnostic\spatial-ai-camera-diagnostic.apk
adb shell am start -n com.spatialai.diagnostics.camera/.MainActivity
```

首次启动允许相机权限。点 **Next camera** 逐个选择 Camera2 ID；灰度预览是该 ID 的实际图像。可用遮挡镜头的办法确认物理映射。**Restart camera** 和 **Stop camera** 用于验证相机资源释放。诊断样例关闭时会关闭相机会话和订阅。

左侧 Consumer A 是预览消费者，Consumer B 模拟算法消费者，每收到五帧会停顿 80 ms。两个消费者显示帧号、采集时间戳与图像校验值；相同帧号应有相同的时间戳和校验值。B 丢掉旧帧时 A 应继续更新。`Pool drops` 表示缓冲池暂时耗尽，并非消费者主动丢弃过时帧。

```powershell
adb logcat -s SpatialCamera:I AndroidRuntime:E '*:S'
```

当前 RhinoX 真机初测：Android 报告 4 个 Camera2 ID；相机特征显示 `id=2` 为 BGGR 彩色传感器，`id=0/1/3` 为 MONO。`id=2/3` 可用 640×480 YUV 连续采集。`id=0/1` 在初次打开时返回 `CAMERA_IN_USE`，原因和镜头归属尚未确定。`id=2` 实测两路消费者收到相同帧号、时间戳和校验值；停止后系统报告相机已关闭，重启及切后台返回后可恢复出帧。

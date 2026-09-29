# RhinoX MediaPipe 手部跟踪测试

这是独立 Android 真机诊断应用：复用 `runtime/platform/android/camera/CameraService.java`，由 RGB 相机 `id=2` 采集 1920×1080，预览走同一 Camera2 会话的 Surface；帧处理线程把 I420 图像缩为 640×360，再送入 MediaPipe Hand Landmarker。屏幕右侧会在手上绘制 21 个关键点与连接线，左侧显示相机帧率、手部推理帧率、耗时和检测到的手数。

目前这是相机到手部关键点的真机验证，尚未接入 Unity XR Hands/XRI，也没有绝对手部位置标定。镜头前没有手时，`Hands: 0` 是正常结果。

## 一键编译

```powershell
cd E:\wuyc\visor-x\SpatialAISDK
.\tools\BuildHandDiagnostic.ps1 -AndroidPlayerPath 'D:\Unity Hub\2022.3.8f1c1\Editor\Data\PlaybackEngines\AndroidPlayer'
```

脚本使用 Unity 自带的 Java 11、Android SDK 和 Gradle 7.2，固定 MediaPipe Tasks Vision `1.0.0`。官方 `hand_landmarker.task` 模型只下载到忽略的 `build/mediapipe/models/`，并校验 SHA-256；模型与第三方 AAR 均不作为自有源码提交。APK 输出到 `build/samples/android/hand-diagnostic/spatial-ai-hand-diagnostic.apk`。

## 安装与观察

```powershell
adb install -r .\build\samples\android\hand-diagnostic\spatial-ai-hand-diagnostic.apk
adb shell pm grant com.spatialai.diagnostics.hand android.permission.CAMERA
adb shell am start -n com.spatialai.diagnostics.hand/.HandTestActivity
adb logcat -s SpatialHand:I SpatialHand:E AndroidRuntime:E '*:S'
```

把一只手伸到 RhinoX 彩色镜头前，观察 `Hands` 是否从 0 变成 1、右侧是否出现绿色关节点；两只手出现时第二只使用青色。若没有检测到手，先调整光线、距离和手掌方向，并确认手确实进入右侧彩色画面。切到后台时相机与模型均关闭，返回时重新建立。

真机已验证：MediaPipe 模型成功加载，彩色相机约 30 FPS，后台线程的手部推理约 8–9 FPS、单次模型推理约 30 ms；在无人手进入镜头的自动验证场景中 `Hands: 0`。识别率、左右手标签、关键点叠加位置还需要手进入镜头后的实测。

资料：[Google Hand Landmarker Android](https://developers.google.com/edge/mediapipe/solutions/vision/hand_landmarker/android)、[Google 官方模型下载脚本](https://github.com/google-ai-edge/mediapipe-samples/blob/main/examples/hand_landmarker/android/app/download_tasks.gradle)。

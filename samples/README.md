# SDK 示例

| 示例 | 位置 | 用途 | 构建产物 |
| --- | --- | --- | --- |
| Unity XR | [`unity/SpatialXR/`](unity/SpatialXR/README.md) | 双目、头姿、注视交互、RGB 手部预览；另有 IMU 与光学诊断场景 | `build/samples/android/spatialxr.apk` |
| Android IMU | [`android/imu-diagnostic/`](android/imu-diagnostic/README.md) | 独立验证 Android 传感器到 Cardboard EKF | `build/samples/android/imu-diagnostic/` |

Unity 工程中的 `Library/`、`Temp/`、`Logs/`、`UserSettings/` 和 APK 属于本机构建产物，不上传 Git。Android 相机与 MediaPipe 手势诊断示例已移至独立的 [`spatial-ai-android`](http://gitlab.ximmerse.com/visor-x/spatial-ai-android) 仓库。

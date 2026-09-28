# Android IMU / Cardboard EKF 真机诊断

独立 Android 诊断程序，包名 `com.spatialai.diagnostics.imu`。不依赖 Unity，不修改 Spatial Runtime，不是官方 HelloCardboard 样例。

数据链路：Android SensorManager → JNI → 固定版本 Cardboard SensorFusionEkf → 四元数与采样状态显示。使用未校准陀螺仪和加速度计，申请周期 10 ms；没有未校准陀螺仪时显示无效，不静默切换到其他来源。

上游代码直接从固定 checkout 编译，不修改上游；APK 内含 Apache 2.0 许可证和来源记录。仅复用传感器融合与数学代码，不调用需要完整 Java/AAR 初始化的公共 Cardboard API。JNI 适配代码仅用于诊断。

## 构建与安装

Windows，使用 Unity AndroidPlayer 配套的 NDK、OpenJDK、SDK（android-32 / build-tools 32.0.0）：

```powershell
powershell -File tools/BuildImuDiagnostic.ps1 -AndroidPlayerPath '<Editor/Data/PlaybackEngines/AndroidPlayer>'
adb -s <设备序列号> install -r build/samples/android/imu-diagnostic/spatial-ai-imu-diagnostic.apk
adb -s <设备序列号> shell am start -W -n com.spatialai.diagnostics.imu/.MainActivity
adb -s <设备序列号> logcat -s SpatialImu:I
```

本机调试签名保存在被忽略的 build 目录，不用于正式发布。应用无网络/相机/麦克风权限；前台时保持亮屏和采样，离开界面后注销传感器并释放融合状态。

## 状态含义

- INPUT FRESH：两种传感器各至少 20 个已接受样本，最新样本年龄均小于 250 ms。
- FUSION FINITE：融合四元数非 NaN/Infinity 且模长平方与 1 的误差小于 0.002。
- q 是上游 EKF 的 Start → Sensor 旋转；未加入 HeadTracker 视口转换、Unity 坐标转换或 RhinoX 标定。不能作为最终头部姿态接口。
- 失效时不显示旧四元数；Reset fusion 清空状态，需等待新样本。传感器坐标轴和视口坐标轴尚未人工验收。
- 两侧显示相同诊断文字，便于检查左右区域；没有双目投影、光学畸变或 XR Display Provider。

## 已验证

2026-09-28 在 ADB 型号 RhinoX / Android API 29 / arm64 上安装、启动和采样成功。约 100 Hz 输入，首轮保存的日志包含 87 条 fresh/finite 记录，样本年龄 5–16 ms。该数值不是转头到显示的端到端延迟。

按 Home 时记录 PAUSE released=true；重新启动后进程 PID 改变，重新初始化并恢复有效采样。验证了暂停释放和进程重建，尚未验证同一进程内的 Activity 恢复。

屏幕截图确认诊断页面已渲染；透过镜片的可读性、真实转动方向和校准效果待用户确认。2026-09-28 的原始记录已归档到 `build/legacy/pre-source-layout/imu-diagnostic/device-20260928/`；今后诊断 APK 默认在 `build/samples/android/imu-diagnostic/`。

# Demo 最低 Android 版本依赖核查

2026-10-13。对象：当前 `com.spatialai.samples.xr` / `build/samples/android/spatialxr.apk`；验证手机 Xiaomi MI 5X / tiffany，Android 8.1.0、API 27、ARM64。

## 结论

当前完整 Demo 保留 API 28 有实际依据：所用 MediaPipe 原生库依赖 API 28 的 `aligned_alloc`，在这台 API 27 手机上直接加载失败。其 AAR 的 API 24 声明不能代表该版本原生二进制可在 API 24–27 加载。仅降低 Unity/Manifest 的最低版本无法使手势功能可用。

IMU/双目显示的主要依赖支持更低版本；可以单独构建 API 26 的 IMU/显示 Demo，保留同一份毫米支架配置。此结论是构建可行性判断，尚未构建或验收低版本 APK。

## 当前依赖证据

读取实际 Gradle `releaseRuntimeClasspath`，检查本地缓存 AAR 的 `AndroidManifest.xml`，并检查 APK 中 ARM64 原生库；不以最新版本替代实际使用版本。

| 依赖 | 声明或官方最低 API | 本轮判断 |
| --- | --- | --- |
| Unity 2022.3 | 22（官方系统要求） | Unity 本身不要求 28；旧手机仍需实际渲染验收 |
| 修改版 Cardboard v1.30.1 / GfxPluginCardboard | AAR 26，NDK 构建目标 26 | 当前原生库在 MI 5X 上 `dlopen` 成功；支持继续做 API 26 IMU 包 |
| UVCAndroid 1.0.13 | AAR 21 | Manifest 不阻挡 API 27；USB 权限、采集与驱动行为仍需真机验证 |
| MediaPipe tasks-vision/tasks-core 1.0.0 | 两个 AAR 均 24 | ARM64 `libmediapipe_tasks_jni.so` 实际编译目标 28，强依赖 `aligned_alloc@LIBC_P`，MI 5X 加载失败 |
| AppCompat 1.6.1 | AAR 14 | 未发现版本门槛 |
| Material 1.12.0 / AndroidX Core 1.13.1 | AAR 19 | 未发现版本门槛 |
| Play Services Vision 20.1.3 | AAR 14 | 未发现版本门槛 |
| 自有 spatial-hand-unity.aar | AAR 28，源 Gradle 28 | 需要重新构建才能改变声明；当前无低版本功能拦截，降声明不解决 MediaPipe 失败 |
| XR Hands 的 libUnityOpenXRHands.so | NDK note 23 | 未发现高于 27 的编译目标；仍需构建和场景运行验证 |

检查当前缓存中的其他 AndroidX/Google AAR，未发现高于 API 24 的第三方 Manifest 门槛。Gradle 实际依赖树生成成功，记录在 `build/validation/android-minsdk/dependencies.txt`。

## MediaPipe 真机加载证据

从当前 Demo APK 提取 MediaPipe 原生库，与 Maven 缓存 `tasks-core:1.0.0` 内库比较，SHA-256 完全相同：

```text
59708f0daa04d24d42e48de7a3c2b73fff12e460004266b026787274effe700a
```

`llvm-readelf --dyn-symbols` 显示非 weak 的强依赖：

```text
GLOBAL DEFAULT UND aligned_alloc@LIBC_P
```

`.note.android.ident` 的编译目标字节为 `1c 00 00 00`，即 28。从 MI 5X 拉取的 `/system/lib64/libc.so` 有 `memalign/posix_memalign`，没有 `aligned_alloc`。使用 API 26 编译的小型 `dlopen(RTLD_NOW)` 工具在手机直接加载 APK 同一库，结果：

```text
LOAD_FAILED: dlopen failed: cannot locate symbol "aligned_alloc" referenced by "/data/local/tmp/spatial-mediapipe-probe.so"...
```

同样的方法加载当前 Cardboard 库得到 `LOAD_OK`。这里只检查动态链接，未调用 JNI_OnLoad 或完整场景；不能把加载成功等同于 IMU/UVC 功能已验收。工具和结果保留在 `build/validation/android-minsdk/`，手机临时探针文件已清理。

## 如要降低版本，需要修改的范围

当前多个构建入口会重写最低版本，单改 Unity Player Settings 会被覆盖：

- `packages/com.spatialai.sdk/Editor/SpatialAndroidSetup.cs`：28。
- `packages/com.spatialai.sdk/Editor/SpatialHandGradleDependencies.cs`：统一重写 launcher/unityLibrary 的 minSdk 为 28，并无条件添加 MediaPipe/UVC 依赖。
- `samples/unity/SpatialXR/Assets/Editor/SpatialXRSetup.cs`：完整 Demo 28。
- `samples/unity/SpatialXR/Assets/Editor/SpatialXRAndroidGradle.cs`：再次重写为 28。
- Android 仓库的 `samples/android/hand-diagnostic/hand-unity/build.gradle`：AAR 28；产物锁文件也记录 28。

手机屏幕读取代码的 API 30 接口已有版本分支，低版本使用 `getDefaultDisplay/getRealMetrics`，API 27 没有发现此处的硬性 API 28 调用。场景广播 API 33 调用也有分支。

可选实施方向：

1. **仅测 IMU/显示**：增加独立 API 26 构建配置，移除手势 AAR/MediaPipe 打包依赖及手势场景，保留 Cardboard、IMU 和同一支架资源。需要让构建回调遵循该配置，然后在 API 27 真机验收。
2. **完整 Demo 在旧机安装，但不提供旧机手势**：除降低自有 AAR/构建声明外，增加 API 28 以下功能拦截，确保任何场景不会初始化 MediaPipe，界面明确显示不可用。仍需确认加载时机和完整启动链。
3. **旧机也运行手势**：更换经验证的低 API 原生 MediaPipe 产物，或用 API 26 工具链重编译并处理高版本接口；重新构建自有 AAR并更新锁文件，验证 CPU/GPU、UVC、手势场景及新旧手机回归。未查明可用替代版本前不直接指定降级版本。

compileSdk/targetSdk 可以继续为 34，与 minSdk 是不同设置。本轮没有修改最低版本配置，也没有重打包或绕过安装检查。

## 官方参考

- [Unity 2022.3 系统要求](https://docs.unity3d.com/2022.3/Documentation/Manual/system-requirements.html)：Unity Android 最低 API 22。
- [Cardboard Unity Quickstart](https://developers.google.com/cardboard/develop/unity/quickstart)：最低 API 26；本地版本也通过 AAR/NDK/真机链接检查核对。
- [MediaPipe Android Setup](https://developers.google.com/edge/mediapipe/solutions/setup_android)：声明 API 24；当前使用的原生二进制另有高版本依赖，以实测结果为准。
- [UVCAndroid 1.0.13 构建配置](https://github.com/shiyinghan/UVCAndroid/blob/1.0.13/build.gradle)：versionMin=21，与实际 AAR 一致。
- [Android Bionic stdlib.h](https://android.googlesource.com/platform/bionic/+/main/libc/include/stdlib.h)：aligned_alloc 标记 `__INTRODUCED_IN(28)`。

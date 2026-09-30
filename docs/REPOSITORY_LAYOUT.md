# Spatial AI SDK 多平台目录设计

状态：2026-09-28 已落实当前源码目录。开发主工程为 `E:\wuyc\visor-x\SpatialAISDK`；`SDK/xms-xr-sdk` 是历史参考工程。WebXR、OpenXR 和 Native SDK 仍是后续规划。

## 一、先定边界

Spatial AI SDK 对应用提供一致的**能力语义**：设备连接、姿态、输入、显示、相机、Spatial Tools、错误、时间戳和权限。各平台实现自己的生命周期与图形提交，不强求同一份渲染代码在 Unity、浏览器和原生应用中运行。

```text
Unity / 团结应用        Web 应用             Native 应用       OpenXR 应用
       │                  │                     │                  │
  Unity Package       Web Package          Native C ABI      OpenXR Runtime* 
       │                  │                     │                  │
       └──────────── 平台能力契约与一致性测试 ──────────────────────┘
                            │
                   Spatial Runtime / 设备服务
                            │
               RhinoX / Cardboard / 后续硬件适配

* OpenXR Runtime 是未来独立项目门槛，不是当前 Unity 插件的别名。
```

WebXR 是浏览器暴露的 `XRSession`、`XRFrame` 等接口。一个 TypeScript SDK 可以调用已存在的浏览器 WebXR；若要让 RhinoX 在浏览器里提供 WebXR，还需要浏览器集成或受控 Web 容器，单有 JS 包不够。[W3C WebXR Device API](https://www.w3.org/TR/webxr/)

OpenXR 区分**应用通过 Loader 调用现有 Runtime**与**我们实现一个供其他应用调用的 Runtime**。前者可以是平台绑定；后者涉及 Loader 集成、图形提交、输入、生命周期、扩展与一致性验证，应独立成模块并在明确产品目标后启动。[Khronos OpenXR 规范](https://registry.khronos.org/OpenXR/specs/1.1-khr/html/xrspec.html)、[Loader 设计](https://registry.khronos.org/OpenXR/specs/1.1/loader.html)

## 二、目标目录

```text
SpatialAISDK/
├─ runtime/                     设备侧共用 Runtime；不引用 Unity/浏览器 API
│  ├─ include/                  稳定 C ABI 与公开数据结构
│  ├─ core/                     会话、能力、时间、坐标、错误、资源所有权
│  ├─ platform/android/         未来 Android 传感器、USB、显示与系统生命周期
│  └─ devices/                  RhinoX、Cardboard 等设备适配及其标定
├─ packages/                    真正交给开发者安装的包
│  ├─ com.spatialai.sdk/        现有 Unity UPM 包；Unity/团结共用 API
│  ├─ web/                      未来 npm/TypeScript 包与浏览器桥接
│  ├─ native/                   未来 Native SDK headers/libs 与打包清单
│  └─ openxr-runtime/           未来 OpenXR Runtime 交付；达到门槛再建
├─ samples/                     可从零运行的消费端工程，按平台划分
│  ├─ unity/
│  ├─ android/imu-diagnostic/   当前独立 IMU 诊断应用
│  ├─ web/                      未来 WebXR 示例
│  └─ native/                   未来 C/C++ 示例
├─ tests/                       能力契约、包导入、平台和真机验收
├─ tools/                       获取依赖、构建、打包、标定转换、验证脚本
├─ third_party/                 固定提交号、许可证、上游源码与补丁记录
├─ docs/                        设计、支持矩阵、开发者文档、验证记录
├─ build/                       忽略的中间产物：编译库、日志、临时工程
└─ dist/                        忽略的本地发布候选包；按版本归档
```

**按需建目录。** 当前源码已归入 `runtime/`、`packages/com.spatialai.sdk/`、`samples/android/imu-diagnostic/`、`tests/`、`tools/` 和 `third_party/`。图中的 Web、OpenXR、Native 包及其他未来模块尚未建立，不生成空工程。`build/legacy/` 保留迁移前的构建记录和未完成下载。

### 发布包的位置

`packages/` 存**安装包的源码**，`build/` 存构建中间文件，`dist/<版本>/<平台>/` 存经过打包和校验、可交付给开发者的**本地发布候选包**。`dist/` 不进 Git；正式发布时再把这些不可变产物上传到选定的包仓库或制品仓库，并记录版本、校验值和许可证。当前尚未选定线上发布地址，也没有执行正式发布。

### `build/` 与共享库

`build/` 是可重新生成的工作区。建议新构建统一按目标平台和架构放置，减少顶层零散目录：

```text
build/
├─ native/host/               当前主机 ABI 测试库
├─ native/android/arm64-v8a/   按组件分目录编译 .so 与 CMake 缓存
│  ├─ spatial-runtime/         libspatial_runtime.so
│  ├─ cardboard/               libGfxPluginCardboard.so
│  └─ imu-diagnostic/          libspatial_imu_diagnostic.so
├─ native/windows/x64/         未来 Windows 原生构建输出
├─ staging/unity/              临时组装 UPM 内容，含需随包交付的 .so
├─ staging/native/             临时组装 Native SDK
├─ tests/                      Unity 导入工程、报告与真机日志
└─ samples/android/            诊断/样例 APK
```

**同一共享库有三个位置，职责不同：**

1. **源码**在 `runtime/`；不能把编译出的 `.so` 当源码维护。
2. **编译结果**在 `build/native/<系统>/<ABI>/<组件>/`；可用于链接、调试和测试，可删除重建。迁移前的历史产物保留在 `build/legacy/pre-source-layout/`。
3. **发布件**由通过验收的二进制复制进 `build/staging/`，再整体打包到 `dist/<版本>/unity/` 或 `dist/<版本>/native/android/`。Unity 的 `.so` 应在 UPM 包内部 `Runtime/Plugins/Android/arm64-v8a/`，并带正确 Unity 插件导入元数据；Native SDK 则连同头文件、许可证和校验清单打包。

第三方预编译库若无法从固定源码构建，才在 `third_party/<供应商>/prebuilt/<平台>/<ABI>/` 保存版本、许可证与校验值。当前 `libGfxPluginCardboard.so` 是上游源码的本地编译结果，仍只在 `build/`；诊断用 `libspatial_imu_diagnostic.so` 也只属于测试 APK，均不自动进入当前 Unity 发布包。WebXR 浏览器包不携带 Android `.so`。

```text
dist/
└─ 0.2.0-preview.5/
   ├─ unity/
   │  ├─ com.spatialai.sdk-0.2.0-preview.5.tgz
   │  ├─ SHA256SUMS.txt
   │  └─ release-manifest.json
   ├─ web/                       未来 npm 包
   ├─ native/android/            未来 Native SDK 压缩包/AAR
   └─ openxr/                   未来具备独立验收的交付件
```

未实现的平台不建立空目录。一个版本的发布包应由同一份锁定源码生成；如内容变化，升级版本后再生成，避免同名包覆盖。样例测试 APK、诊断 APK 与过程 `.so` 留在 `build/`，只有明确列入发布清单的库和示例才进入 `dist/`。

### 每层的职责

| 层 | 放什么 | 不放什么 |
| --- | --- | --- |
| `runtime/` | 设备接入、会话、标定、姿态、显示提交所需的共用原生能力 | Unity `MonoBehaviour`、浏览器 `XRSession`、具体模型供应商 API |
| `packages/` | 各开发环境直接安装的 SDK 包及其平台胶水 | 上游源码全量、样例构建缓存、未交付的占位包 |
| `samples/` | 从干净项目到实际运行的示例；诊断应用也作为独立样例 | 生产 Runtime 的唯一实现 |
| `tests/` | 同一语义契约的测试向量、包导入、断连恢复、双目/姿态真机记录 | 用 Mock 结果冒充硬件通过 |
| `third_party/` | Cardboard 等外部代码的版本锁、许可证、最小补丁 | Spatial 自有稳定 API |

## 三、关键依赖规则

1. `runtime/core` 不依赖 Unity、浏览器、OpenXR 或模型厂商；设备插件依赖 Core。每个应用平台包调用 Runtime 或提供自己的标准平台实现。
2. **共享语义，不假设共享二进制。** 姿态坐标、单位、时间域、能力、错误和权限在 `docs/contracts/` 写规范，在 `tests/contracts/` 放跨语言测试向量。Unity C#、Web TypeScript、Native C 的类型在各自包中实现；Web 端不能直接加载 Android `.so`。
3. 一条运行链路只有一个显示提交与最终畸变所有者。Unity XR Provider、浏览器 WebXR 后端、OpenXR Runtime 各自管理图形生命周期，不能串联成双重提交。
4. `packages/native` 对外提供版本化 C ABI；`runtime/include` 保存源接口。ABI 版本、结构体大小、线程、内存所有权与错误码须可验证。
5. `packages/web` 首先作为浏览器 WebXR 能力的客户端与 Spatial AI 能力客户端。设备侧 WebXR 提供者需另立浏览器桥接模块和真机验收。
6. OpenXR 的两个方向分别命名：未来 `packages/openxr-client/` 使用已有 Runtime，`packages/openxr-runtime/` 实现我们自己的 Runtime。是否需要两者由目标设备和应用分发场景决定，不因目录存在而默认交付。
7. Spatial Tools、模型适配在能力接口稳定后作为可选 Runtime/服务模块；企业账号、支付与应用分发属于服务端产品，不塞进当前设备 SDK 的核心包。

## 四、已完成的目录迁移

| 原路径 | 当前位置 | 说明 |
| --- | --- | --- |
| `packages/com.spatialai.sdk/` | 原位保留 | 当前 Unity 包入口不变 |
| `native/include`、`native/src` | `runtime/include`、`runtime/core` | C ABI 与 Core 源码；原生 ABI 测试移至 `tests/NativeAbi/` |
| `adapters/cardboard`、`adapters/rhinox-pro` | `runtime/devices/...` | 现有接入说明与代码原样归位 |
| `profiles/rhinox-pro.reference.json` | `runtime/devices/rhinox-pro/profiles/reference.json` | 仍是未经实机标定的参考数据 |
| `diagnostics/android-imu/` | `samples/android/imu-diagnostic/` | 独立诊断应用源码 |
| `scripts/` | `tools/` | 构建和验证入口均已更新 |
| `upstream/` | `third_party/` | 版本锁与完整的稀疏检出移入此处 |
| 原 `build/` 下的散列目录 | `build/legacy/pre-source-layout/` | 仅作历史记录，新构建使用分层输出 |

## 五、后续开发顺序

1. **现在**：新功能按现有目录分层放置；保留 `build/legacy/` 以便查证迁移前的结果。
2. **V1 Unity/团结**：打通设备连接、头姿、双目和手柄；完成两个编辑器的独立包导入与 RhinoX 验收。
3. **Native SDK**：稳定 C ABI 和 Android 生命周期后，建立 `packages/native`，提供可安装产物与最小 C/C++ 示例。
4. **WebXR**：先验证目标浏览器是否提供 WebXR；若没有，明确浏览器桥接方案与授权/安全上下文，再建立 Web 包和示例。
5. **OpenXR**：明确是消费现有 OpenXR Runtime 还是实现 Spatial OpenXR Runtime。实现后者时单独开展 Loader、图形、输入、扩展、性能和一致性测试计划。

后续新增平台先确定能力契约与构建入口，再建对应目录。发布包仅从已验证的构建结果组装。

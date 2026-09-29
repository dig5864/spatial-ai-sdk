# 设备 ID 与光学 Profile

Unity 侧的 `SpatialDeviceProfiles.ReadHostIdentity()` 在 Android 读取 `Build.MANUFACTURER`、`Build.MODEL`、`Build.DEVICE`；在 iOS 使用 Unity 提供的 `SystemInfo.deviceModel` 作为手机型号，并标记厂商为 Apple。这些值描述运行 Unity 的手机，不是外接眼镜的身份。对于外接眼镜，必须由设备连接服务取得眼镜本身的 ID，并传入 `headsetDeviceId`。不用手机的唯一标识代替眼镜 ID。

Profile 放在 UPM 包的 `Runtime/Unity/Resources/SpatialAI/Profiles/`。`deviceIds` 使用设备服务给出的稳定眼镜型号/ID；手机插入式 Cardboard 的诊断 Profile 可使用核对过的 Android `Build.DEVICE` 或 iOS `SystemInfo.deviceModel`。选择器大小写不敏感，但只做完整字符串匹配；外接眼镜 ID 优先。未知 ID、重复命中、缺失关键元数据或未验证标定均返回失败，不启用猜测的默认光学参数。

```json
{
  "schemaVersion": 1,
  "profileId": "example-calibration-version",
  "deviceModel": "Example",
  "deviceIds": ["verified-device-id"],
  "calibrationVersion": "1.0.0",
  "validatedForHardware": false,
  "eyeWidth": 0,
  "eyeHeight": 0,
  "screenWidth": 0,
  "screenHeight": 0,
  "ipdMetres": 0,
  "frustumNearMetres": 0,
  "frustumLeftMetres": 0,
  "frustumRightMetres": 0,
  "frustumTopMetres": 0,
  "frustumBottomMetres": 0,
  "meshId": "",
  "meshAssetHash": ""
}
```

上例只是字段说明，不能投入显示。RhinoX Pro 的参考 Profile 已填入历史工程中的 4320×2160 屏幕、2160×2160 单眼、62 mm IPD、非对称视锥和 Sharp 2K 双眼 OST 网格哈希。`SpatialCardboardBridge` 按设备 ID 选择并校验这些字段，交给改造的 Cardboard 显示层；该层对 RhinoX 跳过 QR 镜片参数，直接使用 Profile 的投影和历史网格，避免双重畸变。网格 ID 与哈希须和原生库中的资产一致，否则拒绝配置。显示分辨率不符时原生层拒绝提交网格。

此 Profile 的 `validatedForHardware=false` 仍表示它只是历史参考，尚未完成每台设备的光学校准。已连接的 RhinoX 开发机可用它运行显示对比；不能将它标为已完成量产标定。眼睛相对追踪器的外参、每台设备的校准文件、屏幕方向及其他机型的参数仍需单独取得和验证。未知设备不会推断为 RhinoX；其他 Cardboard 设备继续使用自己的 QR 镜片参数。

## RhinoX Pro IMU 到眼中心设计值

同一个设备 Profile 的 `trackingCalibration` 字段现在保存 `Param_RhinoXPro.cpp` 的设计默认值：EyeRender→Design 绕 X 轴 `-2.3°`、Y 偏移 `-0.01 m`，DesignEye→IMU 平移 `(-0.0400040, 0.0607361, -0.0840374) m`。原生层按旧工程的乘法顺序构造眼中心←IMU 变换，转换到 Unity 坐标并取逆，然后由 Cardboard XR 输入 Provider 发布眼中心姿态。双眼显示 Provider 只继续施加 `±IPD/2`，不会再施加这组 IMU 外参。

Cardboard 原来的固定颈部模型在 RhinoX 设备上由设计外参替代。当前仍只有 3DoF 姿态：位置仅表示转头造成的固定杆臂位移，并在名义姿态归零；它不是 6DoF 位置追踪。`trackingCalibration.validatedForHardware=false`，因为尚未取得设备上的 `/sdcard/vio_fusion_configs.json` 覆盖值，也未做转头、俯仰、回正的真机验证。其他 Cardboard 设备继续使用原来的追踪方式。

Android 原始陀螺仪/加速度计先由 Cardboard 融合，再按屏幕方向转成 Cardboard 头部坐标，随后转成 Unity 坐标。旧 SXR 使用另一套横屏变换；不能把两者的原始传感器轴视为相同。`poseFrame` 固定为 `cardboard-unity-landscape-left`，`designImuFromCardboardHead` 四元数作为显式轴映射。初版单位四元数是两套输出头部坐标一致的**待验证假设**，`axisMappingVerified=false`。RhinoX 运行时限制为横屏左；若实机发现某轴方向或符号不同，先通过转头/俯仰/侧倾测出该映射，再改 Profile 中的四元数，不能通过场景相机旋转修正。

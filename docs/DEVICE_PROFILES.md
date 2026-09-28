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
  "ipdMetres": 0,
  "meshAssetHash": ""
}
```

上例只是字段说明，不能投入显示。启用前需要给出设备/固件匹配、每眼投影、眼位、视口、OST 网格、屏幕方向与分辨率、资产校验值和实机标定记录。当前 `rhinox-pro-reference.json` 是旧工程参考数据，`validatedForHardware=false` 且网格哈希为空。`SpatialCardboardBridge` 只报告匹配状态，不把该 Profile 交给 Cardboard 的 Viewer 参数接口。后续接入眼镜参数时须先明确最终畸变由哪个显示 Provider 执行，避免双重畸变。

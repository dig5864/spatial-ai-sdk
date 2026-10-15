# 小米参考手机的支架基准

2026-10-13 从测试包 `com.spatialai.validation.screen` 导出。手机为 Xiaomi 2206123SC / mayfly；用户用真实尺子确认水平、竖直标尺均约为 50 mm，保存有效屏幕尺寸 145.334×65.430 mm。数值精度由尺子核对精度决定，不能将三位小数理解为精密测量。

`rig.json` 使用现有 xiaomi-mayfly.json 的 2400×1080 参考矩形转换为毫米。屏幕中心偏移沿用参考基准 0/0；仅适用于相同支架、镜片和屏幕安装平面。`referenceScreenMeasured=true` 记录用户标尺确认，`opticsValidated=false` 保留完整光学标定未验收状态。

将 `rig.json` 复制为 Unity 应用的 `Resources/SpatialAI/ScreenRig.json`，或赋给 `SpatialCardboardBridge.phoneRigProfile`，供同一支架上的其他手机使用。其他手机仍需读取并核对自己的有效屏幕尺寸及安装偏移。此文件没有在 SDK 中默认启用，避免影响其他支架项目。

`phone.json` 是本次小米的手机尺寸保存记录，包含保存时间；仅作基准记录。运行时文件按机型 key 的 SHA-256 命名，不能给其他型号直接复用。

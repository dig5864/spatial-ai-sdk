# Hands Simulation

打开 `SpatialHandsSimulation.unity` 后点 Play。左手青色，右手橙色。
点击 Game 窗口后：`1` 张掌、`2` 握拳、`3` 捏合、`L/R` 切换单手跟踪、空格切换运动。
在 `Spatial Hands Simulator` 的 Inspector 单独设置左右手位置、Euler 角与缺失数据。
`Missing Joint` / `Missing Rotation` 的 `Count` 表示不注入丢失。

场景通过标准 `XRHandSubsystem` 接口显示模拟数据，没有使用摄像头或真实手部跟踪。
使用 Built-in Render Pipeline 的 Unity 2022.3 工程运行；URP/HDRP 尚未验证。

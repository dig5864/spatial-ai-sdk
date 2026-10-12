# Real UVC Hands

安装基础 SDK、可选 XR Hands 配套包和 Cardboard 包，配置 Android XR loader 后打开 SpatialUvcXRHands 场景。
示例通过标准 XRHandSubsystem 读取真实 UVC 的米制估算关节；左手青色、右手橙色。
默认无标定只提示、头部局部预览；显示层放远 3 倍并按 Host 设置补偿 X，标准关节不改变。
不把侧掌 depth-held 或漏检保留上报为有效跟踪。精度、光学及长时间实机验收继续待执行。
新建工程也可用 Spatial AI > XR Hands > Create UVC Scene 菜单创建。
详细使用与日志见仓库 docs/UNITY_XR_HANDS_STAGE3.md。

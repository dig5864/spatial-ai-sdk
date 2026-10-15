param([string]$UnityPath='D:\unity\hub\editor\2022.3.14f1\Editor\Unity.exe')
$ErrorActionPreference='Stop'
$sdkRoot=Split-Path -Parent $PSScriptRoot
$project=Join-Path $sdkRoot 'build/validation/physical-scale-unity'
foreach($directory in @('Assets/Editor','Assets/ScaleRuntime','Assets/Resources/SpatialAI','Packages','ProjectSettings')) {
    New-Item -ItemType Directory -Path (Join-Path $project $directory) -Force | Out-Null
}
$utf8=[Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $project 'ProjectSettings/ProjectVersion.txt'), "m_EditorVersion: 2022.3.14f1`n", $utf8)
$deps=@{
    'com.spatialai.sdk'='file:'+(Join-Path $sdkRoot 'packages/com.spatialai.sdk').Replace('\','/')
    'com.google.xr.cardboard'='file:'+(Join-Path $sdkRoot 'third_party/checkouts/cardboard-xr-plugin-unity2022').Replace('\','/')
    'com.unity.xr.management'='4.4.0'
    'com.unity.inputsystem'='1.7.0'
    'com.unity.ugui'='1.0.0'
}
[IO.File]::WriteAllText((Join-Path $project 'Packages/manifest.json'), (@{dependencies=$deps}|ConvertTo-Json), $utf8)
foreach($suffix in @('.cs','.cs.meta')) {
    Copy-Item -LiteralPath (Join-Path $sdkRoot "samples/unity/SpatialXR/Assets/SpatialXR/PhysicalScaleValidation$suffix") -Destination (Join-Path $project "Assets/ScaleRuntime/PhysicalScaleValidation$suffix") -Force
    Copy-Item -LiteralPath (Join-Path $sdkRoot "samples/unity/SpatialXR/Assets/Editor/PhysicalScaleValidationAutomation$suffix") -Destination (Join-Path $project "Assets/Editor/PhysicalScaleValidationAutomation$suffix") -Force
}
Copy-Item -LiteralPath (Join-Path $sdkRoot 'samples/unity/SpatialXR/Assets/SpatialXR/SpatialAI.HelloSpatialXR.asmdef') -Destination (Join-Path $project 'Assets/ScaleRuntime/SpatialAI.HelloSpatialXR.asmdef') -Force
Copy-Item -LiteralPath (Join-Path $sdkRoot 'samples/unity/SpatialXR/Assets/Resources/CalibrationUnlit.shader') -Destination (Join-Path $project 'Assets/Resources/CalibrationUnlit.shader') -Force
Copy-Item -LiteralPath (Join-Path $sdkRoot 'samples/unity/SpatialXR/Assets/Resources/CalibrationUnlit.shader.meta') -Destination (Join-Path $project 'Assets/Resources/CalibrationUnlit.shader.meta') -Force
Copy-Item -LiteralPath (Join-Path $sdkRoot 'docs/calibrations/mayfly-fixed-rig/rig.json') -Destination (Join-Path $project 'Assets/Resources/SpatialAI/ScreenRig.json') -Force
$log=Join-Path $project 'physical-scale-build.log'
$previousSdkRoot=$env:SPATIALAI_SCALE_SDK_ROOT
try {
    $env:SPATIALAI_SCALE_SDK_ROOT=$sdkRoot
    $arguments='-batchmode -nographics -quit -buildTarget Android -projectPath "'+$project+'" -executeMethod PhysicalScaleValidationAutomation.Build -logFile "'+$log+'"'
    $process=Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
} finally { $env:SPATIALAI_SCALE_SDK_ROOT=$previousSdkRoot }
Write-Output "Physical scale build PID=$($process.Id) log=$log"
$deadline=[DateTime]::UtcNow.AddMinutes(20)
while(-not $process.WaitForExit(1000)) {
    if([DateTime]::UtcNow -gt $deadline) { Stop-Process -Id $process.Id; throw "Physical scale build timed out: $log" }
}
if($process.ExitCode -ne 0 -or -not(Select-String -LiteralPath $log -Pattern 'SPATIALAI_SCALE_BUILD_PASS' -Quiet)) {
    Get-Content -LiteralPath $log -Tail 70
    throw "Physical scale build failed: $log"
}
Select-String -LiteralPath $log -Pattern 'SPATIALAI_SCALE_(GEOMETRY|BUILD)_PASS' | ForEach-Object {$_.Line}

param([string]$UnityPath='D:\unity\hub\editor\2022.3.14f1\Editor\Unity.exe', [string]$RigPath='')
$ErrorActionPreference='Stop'
$sdkRoot=Split-Path -Parent $PSScriptRoot
$project=Join-Path $sdkRoot 'build/validation/phone-screen-unity'
foreach($directory in @('Assets/Editor','Packages','ProjectSettings')) {
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
Copy-Item -LiteralPath (Join-Path $sdkRoot 'tests/PhoneScreen/PhoneScreenVerification.cs') -Destination (Join-Path $project 'Assets/Editor/PhoneScreenVerification.cs') -Force
Copy-Item -LiteralPath (Join-Path $sdkRoot 'tests/PhoneScreen/PhoneScreenRuntimeProbe.cs') -Destination (Join-Path $project 'Assets/PhoneScreenRuntimeProbe.cs') -Force
$rigAsset=Join-Path $project 'Assets/Resources/SpatialAI/ScreenRig.json'
if($RigPath) {
    $rigSource=(Resolve-Path -LiteralPath $RigPath).Path
    New-Item -ItemType Directory -Path (Split-Path -Parent $rigAsset) -Force | Out-Null
    Copy-Item -LiteralPath $rigSource -Destination $rigAsset -Force
} elseif(Test-Path -LiteralPath $rigAsset) {
    Remove-Item -LiteralPath $rigAsset
}
$log=Join-Path $project 'verification.log'
$arguments='-batchmode -nographics -quit -buildTarget Android -projectPath "'+$project+'" -executeMethod PhoneScreenVerification.Build -logFile "'+$log+'"'
$process=Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Screen validation PID=$($process.Id) log=$log"
$deadline=[DateTime]::UtcNow.AddMinutes(15)
while(-not $process.WaitForExit(1000)) {
    if([DateTime]::UtcNow -gt $deadline) {
        Stop-Process -Id $process.Id
        throw "Phone screen validation timed out: $log"
    }
}
if($process.ExitCode -ne 0 -or -not(Select-String -LiteralPath $log -Pattern 'SPATIALAI_PHONE_SCREEN_BUILD_PASS' -Quiet)) {
    Get-Content -LiteralPath $log -Tail 70
    throw "Phone screen validation failed: $log"
}
Select-String -LiteralPath $log -Pattern 'SPATIALAI_PHONE_SCREEN_BUILD_PASS' | ForEach-Object {$_.Line}

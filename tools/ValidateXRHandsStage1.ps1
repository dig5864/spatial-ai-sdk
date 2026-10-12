param([Parameter(Mandatory = $true)][string]$UnityPath, [switch]$Graphics)
$ErrorActionPreference = 'Stop'
$sdkRoot = Split-Path -Parent $PSScriptRoot
$utf8 = [Text.UTF8Encoding]::new($false)
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) { throw "Unity not found: $UnityPath" }
$project = Join-Path $sdkRoot ('build/validation/xr-hands-stage1-' + [guid]::NewGuid().ToString('N'))
foreach ($directory in @('Assets/Editor', 'Packages', 'ProjectSettings')) {
    New-Item -ItemType Directory -Path (Join-Path $project $directory) -Force | Out-Null
}
[IO.File]::WriteAllText((Join-Path $project 'ProjectSettings/ProjectVersion.txt'), "m_EditorVersion: 2022.3.14f1`n", $utf8)
$deps = [ordered]@{
    'com.spatialai.sdk' = 'file:' + (Join-Path $sdkRoot 'packages/com.spatialai.sdk').Replace('\','/')
    'com.spatialai.xr-hands' = 'file:' + (Join-Path $sdkRoot 'packages/com.spatialai.xr-hands').Replace('\','/')
    'com.unity.xr.hands' = '1.4.3'
    'com.unity.inputsystem' = '1.7.0'
    'com.unity.xr.management' = '4.4.0'
}
[IO.File]::WriteAllText((Join-Path $project 'Packages/manifest.json'), (@{ dependencies = $deps } | ConvertTo-Json -Depth 4), $utf8)
Copy-Item -LiteralPath (Join-Path $sdkRoot 'tests/XRHandsStage1/Stage1Verification.cs') -Destination (Join-Path $project 'Assets/Editor/Stage1Verification.cs')
foreach ($probe in @('Stage1PlayProbe.cs', 'Stage1LoopProbe.cs', 'Stage1SceneProbe.cs')) {
    Copy-Item -LiteralPath (Join-Path $sdkRoot "tests/XRHandsStage1/$probe") -Destination (Join-Path $project "Assets/$probe")
}
$log = Join-Path $project 'verification.log'
Write-Output "Validating stage 1; log: $log"
# No -quit: the verification method enters/exits Play mode asynchronously and exits Unity itself.
$graphicsArgument = if ($Graphics) { '' } else { ' -nographics' }
$arguments = '-batchmode' + $graphicsArgument + ' -projectPath "' + $project + '" -executeMethod Stage1Verification.Run -logFile "' + $log + '"'
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$deadline = [DateTime]::UtcNow.AddMinutes(5)
while (-not $process.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $deadline) { Stop-Process -Id $process.Id; throw "Verification timed out; inspect $log" }
}
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern 'SPATIALAI_STAGE1_PASS' -Quiet)) {
    throw "Stage 1 verification failed ($($process.ExitCode)); inspect $log"
}
Write-Output "PASS stage 1. Project and playable scene: $project"

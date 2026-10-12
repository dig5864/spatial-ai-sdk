param([Parameter(Mandatory=$true)][string]$UnityPath)
$ErrorActionPreference='Stop'
$sdkRoot=Split-Path -Parent $PSScriptRoot
$project=Join-Path $sdkRoot ('build/validation/xr-hands-stage4-'+[guid]::NewGuid().ToString('N'))
$utf8=[Text.UTF8Encoding]::new($false)
foreach($directory in @('Assets/Editor','Packages','ProjectSettings')){New-Item -ItemType Directory (Join-Path $project $directory) -Force|Out-Null}
[IO.File]::WriteAllText((Join-Path $project 'ProjectSettings/ProjectVersion.txt'),"m_EditorVersion: 2022.3.14f1`n",$utf8)
# Enable Input System before the initial import; Unity supplies the remaining default settings.
[IO.File]::WriteAllText((Join-Path $project 'ProjectSettings/ProjectSettings.asset'),"%YAML 1.1`n%TAG !u! tag:unity3d.com,2011:`n--- !u!129 &1`nPlayerSettings:`n  m_ObjectHideFlags: 0`n  serializedVersion: 26`n  activeInputHandler: 2`n",$utf8)
$deps=[ordered]@{
 'com.spatialai.sdk'='file:'+(Join-Path $sdkRoot 'packages/com.spatialai.sdk').Replace('\','/')
 'com.spatialai.xr-hands'='file:'+(Join-Path $sdkRoot 'packages/com.spatialai.xr-hands').Replace('\','/')
 'com.spatialai.xr-interaction'='file:'+(Join-Path $sdkRoot 'packages/com.spatialai.xr-interaction').Replace('\','/')
 'com.unity.modules.androidjni'='1.0.0'
}
[IO.File]::WriteAllText((Join-Path $project 'Packages/manifest.json'),(@{dependencies=$deps}|ConvertTo-Json -Depth 5),$utf8)
Copy-Item (Join-Path $sdkRoot 'tests/XRHandsStage4/Stage4Verification.cs') (Join-Path $project 'Assets/Editor/Stage4Verification.cs')
Copy-Item (Join-Path $sdkRoot 'tests/XRHandsStage4/Stage4PlayProbe.cs') (Join-Path $project 'Assets/Stage4PlayProbe.cs')
$log=Join-Path $project 'verification.log'
$importLog=Join-Path $project 'gestures-import.log'
$importProcess=Start-Process $UnityPath -ArgumentList ('-batchmode -nographics -quit -projectPath "'+$project+'" -executeMethod SpatialAI.XRInteraction.Editor.SpatialHandInteractionScene.ImportUnityGestures -logFile "'+$importLog+'"') -WindowStyle Hidden -PassThru
Write-Output "Import unchanged Unity XR Hands Gestures; log=$importLog"
if(-not $importProcess.WaitForExit(300000)){Stop-Process -Id $importProcess.Id;throw "Gesture sample import timed out: $importLog"}
if($importProcess.ExitCode -ne 0){throw "Gesture sample import failed: $importLog"}
$process=Start-Process $UnityPath -ArgumentList ('-batchmode -nographics -projectPath "'+$project+'" -executeMethod Stage4Verification.Run -logFile "'+$log+'"') -WindowStyle Hidden -PassThru
Write-Output "Stage 4 validation PID=$($process.Id); log=$log"
$deadline=[DateTime]::UtcNow.AddMinutes(5)
while(-not $process.WaitForExit(1000)){if([DateTime]::UtcNow -gt $deadline){Stop-Process -Id $process.Id;throw "Stage 4 validation timed out: $log"}}
if($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern 'SPATIALAI_STAGE4_PASS' -Quiet)){throw "Stage 4 validation failed: $log"}
Write-Output "PASS stage 4: $project"

param([Parameter(Mandatory=$true)][string]$UnityPath)
$ErrorActionPreference='Stop'
$sdkRoot=Split-Path -Parent $PSScriptRoot
$project=Join-Path $sdkRoot ('build/validation/xr-hands-stage3-'+[guid]::NewGuid().ToString('N'))
$utf8=[Text.UTF8Encoding]::new($false)
foreach($directory in @('Assets/Editor','Packages','ProjectSettings')){New-Item -ItemType Directory (Join-Path $project $directory) -Force|Out-Null}
[IO.File]::WriteAllText((Join-Path $project 'ProjectSettings/ProjectVersion.txt'),"m_EditorVersion: 2022.3.14f1`n",$utf8)
$deps=[ordered]@{
 'com.spatialai.sdk'='file:'+(Join-Path $sdkRoot 'packages/com.spatialai.sdk').Replace('\','/')
 'com.spatialai.xr-hands'='file:'+(Join-Path $sdkRoot 'packages/com.spatialai.xr-hands').Replace('\','/')
 'com.unity.xr.hands'='1.4.3';'com.unity.inputsystem'='1.7.0';'com.unity.xr.management'='4.4.0'
}
[IO.File]::WriteAllText((Join-Path $project 'Packages/manifest.json'),(@{dependencies=$deps}|ConvertTo-Json -Depth 5),$utf8)
Copy-Item (Join-Path $sdkRoot 'tests/XRHandsStage3/Stage3Verification.cs') (Join-Path $project 'Assets/Editor/Stage3Verification.cs')
Copy-Item (Join-Path $sdkRoot 'tests/XRHandsStage3/Stage3PlayProbe.cs') (Join-Path $project 'Assets/Stage3PlayProbe.cs')
$log=Join-Path $project 'verification.log'
$process=Start-Process $UnityPath -ArgumentList ('-batchmode -nographics -projectPath "'+$project+'" -executeMethod Stage3Verification.Run -logFile "'+$log+'"') -WindowStyle Hidden -PassThru
Write-Output "Stage 3 validation PID=$($process.Id); log=$log"
$deadline=[DateTime]::UtcNow.AddMinutes(5)
while(-not $process.WaitForExit(1000)){if([DateTime]::UtcNow -gt $deadline){Stop-Process -Id $process.Id;throw "Stage 3 validation timed out: $log"}}
if($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern 'SPATIALAI_STAGE3_PASS' -Quiet)){throw "Stage 3 validation failed: $log"}
Write-Output "PASS stage 3: $project"

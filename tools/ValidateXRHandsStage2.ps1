param([Parameter(Mandatory=$true)][string]$UnityPath)
$ErrorActionPreference='Stop'
$sdkRoot=Split-Path -Parent $PSScriptRoot
if(-not(Test-Path -LiteralPath $UnityPath -PathType Leaf)){throw "Unity not found: $UnityPath"}
$project=Join-Path $sdkRoot ('build/validation/xr-hands-stage2-'+[guid]::NewGuid().ToString('N'))
$utf8=[Text.UTF8Encoding]::new($false)
foreach($directory in @('Assets/Editor','Packages','ProjectSettings')){New-Item -ItemType Directory -Path (Join-Path $project $directory) -Force|Out-Null}
[IO.File]::WriteAllText((Join-Path $project 'ProjectSettings/ProjectVersion.txt'),"m_EditorVersion: 2022.3.14f1`n",$utf8)
$deps=@{'com.spatialai.sdk'='file:'+(Join-Path $sdkRoot 'packages/com.spatialai.sdk').Replace('\','/')}
[IO.File]::WriteAllText((Join-Path $project 'Packages/manifest.json'),(@{dependencies=$deps}|ConvertTo-Json),$utf8)
Copy-Item -LiteralPath (Join-Path $sdkRoot 'tests/CoreChecks/UvcGeometryChecks.cs') -Destination (Join-Path $project 'Assets/Editor/UvcGeometryChecks.cs')
Copy-Item -LiteralPath (Join-Path $sdkRoot 'tests/XRHandsStage2/Stage2Verification.cs') -Destination (Join-Path $project 'Assets/Editor/Stage2Verification.cs')
$log=Join-Path $project 'verification.log'
$arguments='-batchmode -nographics -quit -projectPath "'+$project+'" -executeMethod Stage2Verification.Run -logFile "'+$log+'"'
Write-Output "Stage 2 log: $log"
$process=Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$deadline=[DateTime]::UtcNow.AddMinutes(5)
while(-not $process.WaitForExit(1000)){if([DateTime]::UtcNow -gt $deadline){$process.Kill();throw "Stage 2 timed out: $log"}}
if($process.ExitCode -ne 0 -or -not(Select-String -LiteralPath $log -Pattern 'SPATIALAI_STAGE2_PASS' -Quiet)){throw "Stage 2 failed ($($process.ExitCode)): $log"}
Select-String -LiteralPath $log -Pattern 'SPATIALAI_STAGE2_PASS'|ForEach-Object{$_.Line}
Write-Output "Verified project: $project"

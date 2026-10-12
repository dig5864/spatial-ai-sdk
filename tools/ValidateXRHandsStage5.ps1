param([Parameter(Mandatory=$true)][string]$UnityPath,[string]$ReleaseDirectory='', [switch]$SkipAndroidBuild, [string]$ValidationRoot='')
$ErrorActionPreference='Stop'
$sdkRoot=Split-Path -Parent $PSScriptRoot
if (!$ReleaseDirectory) {$version=(Get-Content (Join-Path $sdkRoot 'packages/com.spatialai.sdk/package.json') -Raw|ConvertFrom-Json).version;$ReleaseDirectory=Join-Path $sdkRoot "dist/$version/unity"}
if (-not $ValidationRoot) {
    & python (Join-Path $PSScriptRoot 'PrepareXRHandsStage5.py') $ReleaseDirectory
    if($LASTEXITCODE -ne 0){throw 'Release verification and project preparation failed'}
    $ValidationRoot=Get-Content (Join-Path $sdkRoot 'build/stage5-validation-path.txt') -Raw
}
$validationRoot=[IO.Path]::GetFullPath($ValidationRoot)
foreach ($mode in @('full','display','upgrade')) {
    Copy-Item -LiteralPath (Join-Path $sdkRoot 'tests/UnityInstaller/InstallerVerification.cs') -Destination (Join-Path $validationRoot "$mode/Assets/Editor/InstallerVerification.cs") -Force
}
Copy-Item (Join-Path $sdkRoot 'tests/XRHandsStage5/Stage5DisplayVerification.cs') (Join-Path $validationRoot 'full/Assets/Editor/Stage5DisplayVerification.cs') -Force
Copy-Item (Join-Path $sdkRoot 'tests/XRHandsStage5/Stage5Verification.cs') (Join-Path $validationRoot 'full/Assets/Editor/Stage5Verification.cs') -Force
Copy-Item (Join-Path $sdkRoot 'tests/XRHandsStage5/Stage5PlayProbe.cs') (Join-Path $validationRoot 'full/Assets/Stage5PlayProbe.cs') -Force
function Run-UnityPhase([string]$project,[string]$method,[string]$name,[string]$pass='', [switch]$Quit) {
    $log=Join-Path $project ($name+'.log')
    $arguments='-batchmode -nographics -projectPath "'+$project+'" -executeMethod '+$method+' -logFile "'+$log+'"'
    if($Quit){$arguments+=' -quit'}
    $process=Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Output "Stage5 $name PID=$($process.Id) log=$log"
    $deadline=[DateTime]::UtcNow.AddMinutes(15)
    while(-not $process.WaitForExit(1000)){if([DateTime]::UtcNow -gt $deadline){Stop-Process -Id $process.Id;throw "Timed out: $log"}}
    if($process.ExitCode -ne 0 -or ($pass -and -not (Select-String -LiteralPath $log -Pattern $pass -Quiet))){throw "Stage5 $name failed: $log"}
    Write-Output "PASS $name"
}
$full=Join-Path $validationRoot 'full'
$display=Join-Path $validationRoot 'display'
$upgrade=Join-Path $validationRoot 'upgrade'
Run-UnityPhase $full 'InstallerVerification.WaitUntilReady' 'sdk-auto-install' 'SPATIALAI_INSTALLER_READY'
Run-UnityPhase $display 'InstallerVerification.WaitUntilReady' 'sdk-auto-install' 'SPATIALAI_INSTALLER_READY'
Run-UnityPhase $full 'SpatialAI.XRInteraction.Editor.SpatialHandInteractionScene.ImportUnityGestures' 'gestures-import' -Quit
Run-UnityPhase $full 'Stage5Verification.Run' 'import-and-play' 'SPATIALAI_STAGE5_PLAY_PASS'
Run-UnityPhase $display 'Stage5DisplayVerification.Run' 'display-only' 'SPATIALAI_STAGE5_DISPLAY_PASS'
$upgradeManifestPath=Join-Path $upgrade 'Packages/manifest.json'
$upgradeManifest=Get-Content $upgradeManifestPath -Raw|ConvertFrom-Json
$upgradeManifest.dependencies.'com.spatialai.sdk'='file:../../SDKRelease/com.spatialai.sdk-baseline.tgz'
[IO.File]::WriteAllText($upgradeManifestPath,($upgradeManifest|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
Run-UnityPhase $upgrade 'Stage5UpgradeBaseline.Run' 'upgrade-baseline' 'SPATIALAI_STAGE5_UPGRADE_BASELINE_PASS' -Quit
$releaseManifest=Get-Content (Join-Path $validationRoot 'SDKRelease/release-manifest.json') -Raw|ConvertFrom-Json
$upgradeManifest.dependencies.'com.spatialai.sdk'='file:../../SDKRelease/'+($releaseManifest.packages|Where-Object name -eq 'com.spatialai.sdk').file
[IO.File]::WriteAllText($upgradeManifestPath,($upgradeManifest|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
Copy-Item (Join-Path $sdkRoot 'tests/XRHandsStage5/Stage5UpgradeVerification.cs') (Join-Path $upgrade 'Assets/Editor/Stage5UpgradeVerification.cs')
Run-UnityPhase $upgrade 'InstallerVerification.WaitUntilReady' 'sdk-auto-install' 'SPATIALAI_INSTALLER_READY'
Run-UnityPhase $upgrade 'Stage5UpgradeVerification.Run' 'upgrade' 'SPATIALAI_STAGE5_UPGRADE_PASS' -Quit
if(-not $SkipAndroidBuild){
    Run-UnityPhase $full 'SpatialAI.Editor.SpatialAndroidSetup.Build' 'android-build' 'SPATIALAI_CONSUMER_APK' -Quit
    Run-UnityPhase $display 'SpatialAI.Editor.SpatialAndroidSetup.Build' 'android-build' 'SPATIALAI_CONSUMER_APK' -Quit
}
Write-Output "SPATIALAI_STAGE5_PASS androidBuild=$(-not $SkipAndroidBuild) validationRoot=$validationRoot"

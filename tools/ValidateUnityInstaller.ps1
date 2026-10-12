param(
    [Parameter(Mandatory=$true)][string]$UnityPath,
    [string]$ReleaseDirectory='',
    [string]$LegacyInstaller=''
)
$ErrorActionPreference='Stop'
$sdkRoot=Split-Path -Parent $PSScriptRoot
if (-not $ReleaseDirectory) {
    $version=(Get-Content (Join-Path $sdkRoot 'packages/com.spatialai.sdk/package.json') -Raw | ConvertFrom-Json).version
    $ReleaseDirectory=Join-Path $sdkRoot "dist/$version/unity"
}
$prepareArgs=@((Join-Path $PSScriptRoot 'PrepareUnityInstallerValidation.py'),$ReleaseDirectory)
if ($LegacyInstaller) { $prepareArgs+=@('--legacy-installer',$LegacyInstaller) }
& python @prepareArgs
if ($LASTEXITCODE -ne 0) { throw 'Installer payload validation failed.' }
$validationRoot=Get-Content (Join-Path $sdkRoot 'build/installer-validation-path.txt') -Raw
$consumer=Join-Path $validationRoot 'consumer'
function Run-InstallerCheck([string]$project,[string]$method,[string]$marker,[switch]$Quit) {
    $log=Join-Path $project 'installer-validation.log'
    $arguments='-batchmode -nographics -projectPath "'+$project+'" -executeMethod '+$method+' -logFile "'+$log+'"'
    if ($Quit) { $arguments+=' -quit' }
    $process=Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Output "Installer validation PID=$($process.Id) project=$project"
    $deadline=[DateTime]::UtcNow.AddMinutes(8)
    while (-not $process.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -gt $deadline) { Stop-Process -Id $process.Id; throw "Validation timed out: $log" }
    }
    if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch $marker -Quiet)) {
        throw "Installer validation failed: $log"
    }
}
$startMethod=if ($LegacyInstaller) { 'InstallerVerification.StartLegacyUpgrade' } else { 'InstallerVerification.Start' }
Run-InstallerCheck $consumer $startMethod 'SPATIALAI_INSTALLER_PASS'
$relocated=Join-Path $validationRoot 'relocated project'
New-Item -ItemType Directory -Path $relocated | Out-Null
foreach ($directory in @('Assets','Packages','ProjectSettings','SDKPackages')) {
    Copy-Item -LiteralPath (Join-Path $consumer $directory) -Destination $relocated -Recurse
}
Copy-Item -LiteralPath (Join-Path $consumer 'installer-probe.json') -Destination $relocated
Run-InstallerCheck $relocated 'InstallerVerification.VerifyRelocated' 'SPATIALAI_INSTALLER_RELOCATION_PASS' -Quit
Write-Output "SPATIALAI_INSTALLER_VALIDATION_PASS root=$validationRoot"

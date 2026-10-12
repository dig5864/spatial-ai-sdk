param(
    [Parameter(Mandatory = $true)][string]$UnityPath,
    [switch]$IncludeCardboard
)
$ErrorActionPreference = 'Stop'
$sdkRoot = Split-Path -Parent $PSScriptRoot
$utf8 = [Text.UTF8Encoding]::new($false)
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) { throw "Unity not found: $UnityPath" }
# Each run gets fresh projects. No existing Unity checkout or package lock is altered.
$runRoot = Join-Path $sdkRoot ('build/validation/xr-hands-stage0-' + [guid]::NewGuid().ToString('N'))
foreach ($variant in @('sdk-only', 'with-xr-hands')) {
    $project = Join-Path $runRoot $variant
    foreach ($directory in @('Assets/Editor', 'Packages', 'ProjectSettings')) {
        New-Item -ItemType Directory -Path (Join-Path $project $directory) -Force | Out-Null
    }
    # A missing version file makes Unity add its complete default template packages.
    [IO.File]::WriteAllText((Join-Path $project 'ProjectSettings/ProjectVersion.txt'), "m_EditorVersion: 2022.3.14f1`n", $utf8)
    $deps = [ordered]@{ 'com.spatialai.sdk' = 'file:' + (Join-Path $sdkRoot 'packages/com.spatialai.sdk').Replace('\','/') }
    if ($variant -eq 'with-xr-hands') {
        $deps['com.spatialai.xr-hands'] = 'file:' + (Join-Path $sdkRoot 'packages/com.spatialai.xr-hands').Replace('\','/')
        # Direct pins establish a reproducible consumer baseline as well as package minima.
        $deps['com.unity.xr.hands'] = '1.4.3'
        $deps['com.unity.inputsystem'] = '1.7.0'
        $deps['com.unity.xr.management'] = '4.4.0'
        if ($IncludeCardboard) {
            $cardboard = Join-Path $sdkRoot 'third_party/checkouts/cardboard-xr-plugin-unity2022'
            if (-not (Test-Path -LiteralPath (Join-Path $cardboard 'package.json'))) { throw 'Prepare Cardboard with BuildRhinoXCardboard.ps1 first.' }
            $deps['com.google.xr.cardboard'] = 'file:' + $cardboard.Replace('\','/')
            # CardboardReticlePointer imports UnityEngine.EventSystems.
            $deps['com.unity.ugui'] = '1.0.0'
        }
        [IO.File]::WriteAllText((Join-Path $project 'Assets/Editor/Stage0CompileProbe.cs'),
            "#define SPATIALAI_STAGE0_WITH_XR_HANDS`n" + [IO.File]::ReadAllText((Join-Path $sdkRoot 'tests/XRHandsStage0/Stage0CompileProbe.cs')), $utf8)
    } else {
        Copy-Item -LiteralPath (Join-Path $sdkRoot 'tests/XRHandsStage0/Stage0CompileProbe.cs') -Destination (Join-Path $project 'Assets/Editor/Stage0CompileProbe.cs')
    }
    [IO.File]::WriteAllText((Join-Path $project 'Packages/manifest.json'), (@{ dependencies = $deps } | ConvertTo-Json -Depth 4), $utf8)
    $log = Join-Path $project 'compile.log'
    Write-Output "Validating $variant; log: $log"
    $arguments = '-batchmode -nographics -quit -projectPath "' + $project + '" -executeMethod Stage0CompileProbe.Run -logFile "' + $log + '"'
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    while (-not $process.WaitForExit(1000)) { }
    if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern 'SPATIALAI_STAGE0_PASS' -Quiet)) {
        throw "Unity verification failed ($($process.ExitCode)); inspect $log"
    }
    if ($variant -eq 'with-xr-hands') {
        $lock = Get-Content -LiteralPath (Join-Path $project 'Packages/packages-lock.json') -Raw | ConvertFrom-Json
        foreach ($pin in @('com.unity.xr.hands','com.unity.inputsystem','com.unity.xr.management')) {
            if ($lock.dependencies.$pin.version -ne $deps[$pin]) { throw "Resolved version differs from pin: $pin" }
        }
    } else {
        $lock = Get-Content -LiteralPath (Join-Path $project 'Packages/packages-lock.json') -Raw | ConvertFrom-Json
        if ($lock.dependencies.'com.unity.xr.hands') { throw 'Base SDK unexpectedly requires XR Hands.' }
    }
    Write-Output "PASS $variant"
}
Write-Output "Stage 0 projects and dependency locks: $runRoot"

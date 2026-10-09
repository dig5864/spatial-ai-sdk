param(
    [Parameter(Mandatory=$true)][string]$NdkPath,
    [switch]$SkipUnityPluginInstall,
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$native = Join-Path $root 'third_party/checkouts/cardboard-sdk-v1.30.1'
$plugin = Join-Path $root 'third_party/checkouts/cardboard-xr-plugin-unity2022'
$mesh = Join-Path $root 'third_party/rhinox/spatial_rhinox_mesh_data.cc'
$patch = Join-Path $root 'third_party/rhinox/cardboard-display-api.patch'
$nativeMesh = Join-Path $native 'sdk/unity/xr_unity_plugin/spatial_rhinox_mesh_data.cc'
$build = Join-Path $root 'build/native/cardboard-rhinox-v1301'
$aar = Join-Path $build 'GfxPluginCardboard-rhinox.aar'
$appliedPatch = Join-Path $build 'applied-cardboard.patch'
$upstreamAar = Join-Path $plugin 'Runtime/Android/GfxPluginCardboard.aar'
$toolchain = Join-Path $NdkPath 'build/cmake/android.toolchain.cmake'
$meshHash = 'sha256:' + (Get-FileHash -LiteralPath $mesh -Algorithm SHA256).Hash.ToLowerInvariant()
$profilePath = Join-Path $root 'packages/com.spatialai.sdk/Runtime/Unity/Resources/SpatialAI/Profiles/rhinox-pro-reference.json'
$profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
if ($profile.meshAssetHash -ne $meshHash -or !(Select-String -LiteralPath $patch -SimpleMatch $meshHash -Quiet)) {
    throw "RhinoX mesh identity differs between source, native patch and optical profile: $meshHash"
}
$tracking = $profile.trackingCalibration
if ($null -eq $tracking -or $tracking.source -notmatch 'Param_RhinoXPro' -or
    $tracking.eyeRenderToDesignDegreesX -ne -2.3 -or
    $tracking.eyeRenderToDesignYOffsetMetres -ne -0.01 -or
    $tracking.designEyeToImuXMetres -ne -0.0400040 -or
    $tracking.designEyeToImuYMetres -ne 0.0607361 -or
    $tracking.designEyeToImuZMetres -ne -0.0840374 -or
    $tracking.poseFrame -ne 'cardboard-unity-landscape-left') {
    throw 'RhinoX IMU-to-eye design reference differs from Param_RhinoXPro.cpp defaults.'
}

if (!(Test-Path -LiteralPath $toolchain)) { throw "NDK toolchain missing: $toolchain" }
foreach ($command in @('git', 'cmake', 'ninja', 'python')) {
    if (!(Get-Command $command -ErrorAction SilentlyContinue)) { throw "Build dependency missing from PATH: $command" }
}
if ($CheckOnly) {
    Write-Host "RhinoX dependency checks passed; NDK: $NdkPath"
    return
}
if (!(Test-Path -LiteralPath $native)) {
    git clone --depth 1 --branch v1.30.1 https://github.com/googlevr/cardboard.git $native
    if ($LASTEXITCODE -ne 0) { throw 'Could not fetch Cardboard v1.30.1.' }
}
$revision = (git -C $native rev-parse HEAD).Trim()
if ($revision -ne '67bee10850562bf8cbe563a22b47eca8ffd8f1eb') {
    throw "Unexpected Cardboard source revision: $revision"
}
if (Test-Path -LiteralPath $nativeMesh) {
    if ((Get-FileHash -LiteralPath $nativeMesh -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $mesh -Algorithm SHA256).Hash) {
        Copy-Item -LiteralPath $mesh -Destination $nativeMesh -Force
        (Get-Item -LiteralPath $nativeMesh).LastWriteTime = Get-Date
    }
} else {
    Copy-Item -LiteralPath $mesh -Destination $nativeMesh
    (Get-Item -LiteralPath $nativeMesh).LastWriteTime = Get-Date
}
function Test-CardboardPatch([string]$path, [switch]$Reverse) {
    # Windows PowerShell can turn an expected native stderr into a terminating
    # error under Stop. Only suppress errors for these non-mutating checks.
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $arguments = @('-C', $native, 'apply', '--check')
        if ($Reverse) { $arguments += '--reverse' }
        & git @arguments $path 2>$null
        return $LASTEXITCODE -eq 0
    } finally { $ErrorActionPreference = $previousPreference }
}
if (!(Test-CardboardPatch $patch -Reverse)) {
    $replacedPatch = $false
    if (Test-Path -LiteralPath $appliedPatch) {
        if (Test-CardboardPatch $appliedPatch -Reverse) {
            git -C $native apply --reverse $appliedPatch
            if ($LASTEXITCODE -ne 0) { throw 'Could not remove the previous generated Cardboard patch.' }
            $replacedPatch = $true
        }
    }
    if (!(Test-CardboardPatch $patch)) {
        if ($replacedPatch) { git -C $native apply $appliedPatch }
        throw 'Cardboard patch does not apply cleanly. Check local native source edits; they have not been discarded.'
    }
    git -C $native apply $patch
    if ($LASTEXITCODE -ne 0) { throw 'Could not apply RhinoX Cardboard patch.' }
}
New-Item -ItemType Directory -Path $build -Force | Out-Null
Copy-Item -LiteralPath $patch -Destination $appliedPatch -Force

cmake -S (Join-Path $native 'sdk') -B $build -G Ninja "-DCMAKE_TOOLCHAIN_FILE=$toolchain" -DANDROID_USE_LEGACY_TOOLCHAIN_FILE=ON -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-26 -DCARDBOARDSDK_RENDERING_VULKAN=ON
if ($LASTEXITCODE -ne 0) { throw 'Cardboard configure failed.' }
cmake --build $build -j 8
if ($LASTEXITCODE -ne 0) { throw 'Cardboard native build failed.' }
$library = Join-Path $build 'libGfxPluginCardboard.so'
$nm = Join-Path $NdkPath 'toolchains/llvm/prebuilt/windows-x86_64/bin/llvm-nm.exe'
$symbols = & $nm -D $library
if ($LASTEXITCODE -ne 0 -or !($symbols -match 'CardboardUnity_configureSpatialOptics') -or !($symbols -match 'CardboardUnity_clearSpatialOptics') -or !($symbols -match 'CardboardUnity_isSpatialOpticsApplied') -or !($symbols -match 'CardboardUnity_configureSpatialTracking') -or !($symbols -match 'CardboardUnity_getSpatialTracking') -or !($symbols -match 'CardboardUnity_clearSpatialTracking') -or !($symbols -match 'CardboardUnity_setHostViewCompensation')) {
    throw 'RhinoX native API symbols are missing.'
}
if (!(Test-Path -LiteralPath $plugin)) {
    git clone --depth 1 --branch v1.30.1 https://github.com/googlevr/cardboard-xr-plugin.git $plugin
    if ($LASTEXITCODE -ne 0) { throw 'Could not fetch Cardboard XR Plugin v1.30.1.' }
}
if (!(Test-Path -LiteralPath $upstreamAar)) { throw "Cardboard XR Plugin AAR missing: $upstreamAar" }
$package = Get-Content -LiteralPath (Join-Path $plugin 'package.json') -Raw | ConvertFrom-Json
if ($package.version -ne '1.30.0') { throw "Expected Cardboard XR Plugin 1.30.0 package, got $($package.version)." }
python (Join-Path $PSScriptRoot 'ReplaceAarNative.py') $upstreamAar $library $aar
if ($LASTEXITCODE -ne 0) { throw 'Could not package RhinoX AAR.' }
if (!$SkipUnityPluginInstall -and
    (Get-FileHash -LiteralPath $aar -Algorithm SHA256).Hash -ne
    (Get-FileHash -LiteralPath $upstreamAar -Algorithm SHA256).Hash) {
    Copy-Item -LiteralPath $aar -Destination $upstreamAar -Force
}
Write-Host "RhinoX AAR: $aar"
Write-Host "Native library SHA256: $((Get-FileHash -LiteralPath $library -Algorithm SHA256).Hash)"

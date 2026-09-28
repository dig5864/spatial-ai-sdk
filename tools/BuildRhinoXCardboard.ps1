param(
    [string]$NdkPath = 'D:\AndroidSdk\sdk\ndk\24.0.8215888',
    [switch]$SkipUnityPluginInstall
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$native = Join-Path $root 'third_party/checkouts/cardboard-sdk-v1.30.1'
$plugin = Join-Path $root 'third_party/checkouts/cardboard-xr-plugin-unity2022'
$mesh = Join-Path $root 'third_party/rhinox/spatial_rhinox_mesh_data.cc'
$patch = Join-Path $root 'third_party/rhinox/cardboard-display-api.patch'
$nativeMesh = Join-Path $native 'sdk/unity/xr_unity_plugin/spatial_rhinox_mesh_data.cc'
$build = Join-Path $root 'build/native/cardboard-rhinox-v1301'
$aar = Join-Path $root 'third_party/rhinox/GfxPluginCardboard-rhinox.aar'
$upstreamAar = Join-Path $plugin 'Runtime/Android/GfxPluginCardboard.aar'
$toolchain = Join-Path $NdkPath 'build/cmake/android.toolchain.cmake'

if (!(Test-Path -LiteralPath $toolchain)) { throw "NDK toolchain missing: $toolchain" }
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
git -C $native apply --reverse --check $patch 2>$null
if ($LASTEXITCODE -ne 0) {
    git -C $native apply --check $patch
    if ($LASTEXITCODE -ne 0) { throw 'Cardboard patch does not apply cleanly.' }
    git -C $native apply $patch
    if ($LASTEXITCODE -ne 0) { throw 'Could not apply RhinoX Cardboard patch.' }
}

cmake -S (Join-Path $native 'sdk') -B $build -G Ninja "-DCMAKE_TOOLCHAIN_FILE=$toolchain" -DANDROID_USE_LEGACY_TOOLCHAIN_FILE=ON -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-26 -DCARDBOARDSDK_RENDERING_VULKAN=ON
if ($LASTEXITCODE -ne 0) { throw 'Cardboard configure failed.' }
cmake --build $build -j 8
if ($LASTEXITCODE -ne 0) { throw 'Cardboard native build failed.' }
$library = Join-Path $build 'libGfxPluginCardboard.so'
$nm = Join-Path $NdkPath 'toolchains/llvm/prebuilt/windows-x86_64/bin/llvm-nm.exe'
$symbols = & $nm -D $library
if ($LASTEXITCODE -ne 0 -or !($symbols -match 'CardboardUnity_setRhinoXLegacyMesh') -or !($symbols -match 'CardboardUnity_isRhinoXLegacyMeshApplied')) {
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
if (!$SkipUnityPluginInstall) { Copy-Item -LiteralPath $aar -Destination $upstreamAar -Force }
Write-Host "RhinoX AAR: $aar"
Write-Host "Native library SHA256: $((Get-FileHash -LiteralPath $library -Algorithm SHA256).Hash)"

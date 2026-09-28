param([Parameter(Mandatory=$true)][string]$NdkPath, [string]$BuildDirectory = '')
$ErrorActionPreference = 'Stop'
$sdkRoot = Split-Path -Parent $PSScriptRoot
$toolchain = Join-Path $NdkPath 'build/cmake/android.toolchain.cmake'
if (!(Test-Path -LiteralPath $toolchain)) { throw 'Android NDK toolchain not found.' }
$destination = if ($BuildDirectory) { $BuildDirectory } else { Join-Path $sdkRoot 'build/native/android/arm64-v8a/spatial-runtime' }
if (!(Test-Path Env:PROCESSOR_ARCHITECTURE)) {
    throw 'PROCESSOR_ARCHITECTURE is missing in this process. Run from a standard Windows developer shell, or set the correct host architecture before configuring a fresh build directory.'
}
cmake -S (Join-Path $sdkRoot 'runtime') -B $destination -G Ninja "-DCMAKE_TOOLCHAIN_FILE=$toolchain" -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-26 -DSPATIAL_BUILD_TESTS=OFF
if ($LASTEXITCODE -ne 0) { throw 'Android configure failed.' }
cmake --build $destination
if ($LASTEXITCODE -ne 0) { throw 'Android native build failed.' }
Write-Output "Native foundation built at $destination. Hardware adapters remain unsupported."

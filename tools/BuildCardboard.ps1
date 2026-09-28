param([Parameter(Mandatory=$true)][string]$NdkPath, [string]$BuildDirectory = '')
. (Join-Path $PSScriptRoot 'CardboardCommon.ps1')
$sdkRoot = Split-Path -Parent $PSScriptRoot
$lock = Read-CardboardLock $sdkRoot
$entry = $lock.repositories | Where-Object { $_.id -eq 'cardboard' }
$source = Join-Path (Join-Path $sdkRoot 'third_party/checkouts') $entry.directory
Assert-CardboardCheckout $source $entry
$toolchain = Join-Path $NdkPath 'build/cmake/android.toolchain.cmake'
$readelf = Join-Path $NdkPath 'toolchains/llvm/prebuilt/windows-x86_64/bin/llvm-readelf.exe'
foreach ($required in @($toolchain, $readelf, (Join-Path $source 'LICENSE'), (Join-Path $source 'sdk/CMakeLists.txt'), (Join-Path $source 'third_party/unity_plugin_api/IUnityInterface.h'))) {
    if (!(Test-Path -LiteralPath $required)) { throw "Missing build prerequisite: $required" }
}
if (!(Test-Path Env:PROCESSOR_ARCHITECTURE)) {
    throw 'PROCESSOR_ARCHITECTURE is missing. Use a Windows developer shell with the correct host architecture.'
}
$destination = if ($BuildDirectory) { [IO.Path]::GetFullPath($BuildDirectory) } else { Join-Path $sdkRoot 'build/native/android/arm64-v8a/cardboard' }
& cmake -S (Join-Path $source 'sdk') -B $destination -G Ninja "-DCMAKE_TOOLCHAIN_FILE=$toolchain" -DCMAKE_BUILD_TYPE=Release -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-26 -DANDROID_STL=c++_static -DCARDBOARDSDK_UNITY_PLUGIN=ON -DCARDBOARDSDK_RENDERING_GLESv3=ON -DCARDBOARDSDK_RENDERING_VULKAN=ON
if ($LASTEXITCODE -ne 0) { throw 'Cardboard configure failed.' }
& cmake --build $destination --parallel 4
if ($LASTEXITCODE -ne 0) { throw 'Cardboard build failed.' }
$library = Join-Path $destination 'libGfxPluginCardboard.so'
$elfHeader = & $readelf --file-header $library
if ($LASTEXITCODE -ne 0 -or !($elfHeader -match 'AArch64')) { throw 'Output is not an AArch64 ELF library.' }
$symbols = & $readelf --dyn-syms --wide $library
if ($LASTEXITCODE -ne 0) { throw 'Cannot read library exports.' }
$requiredExports = @('Cardboard_initializeAndroid', 'CardboardHeadTracker_create', 'CardboardHeadTracker_getPose', 'CardboardHeadTracker_resume', 'CardboardHeadTracker_pause', 'CardboardHeadTracker_recenter', 'UnityPluginLoad', 'JNI_OnLoad')
foreach ($symbol in $requiredExports) {
    $pattern = '\b' + [regex]::Escape($symbol) + '(@@\S+)?\s*$'
    if (!(($symbols | Where-Object { $_ -notmatch '\bUND\b' }) -match $pattern)) { throw "Missing defined export: $symbol" }
}
$dynamic = & $readelf --dynamic $library
if ($LASTEXITCODE -ne 0) { throw 'Cannot read shared-library dependencies.' }
$report = [ordered]@{
    schemaVersion = 1
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    source = $entry.url
    commit = $entry.commit
    sourceModified = $false
    ndk = [regex]::Match([IO.File]::ReadAllText((Join-Path $NdkPath 'source.properties')), '(?m)^Pkg.Revision\s*=\s*(\S+)').Groups[1].Value
    target = 'android-26/arm64-v8a'
    configuration = 'Release; Unity provider ON; GLES3 ON; Vulkan ON; c++_static'
    library = $library
    sha256 = (Get-FileHash -LiteralPath $library -Algorithm SHA256).Hash.ToLowerInvariant()
    verifiedExports = $requiredExports
    neededLibraries = @($dynamic | Where-Object { $_ -match '\(NEEDED\)' })
    deviceTested = $false
    completeAar = $false
    spatialRuntimeConnected = $false
}
$reportPath = Join-Path $destination 'build-report.json'
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $source 'LICENSE') -Destination (Join-Path $destination 'LICENSE.cardboard') -Force
Write-Output "Cardboard native baseline verified: $library"
Write-Output "Build report: $reportPath. JNI/Java packaging and device validation remain pending."

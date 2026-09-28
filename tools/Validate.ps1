param([string]$UnityManagedDir = '')
$ErrorActionPreference = 'Stop'
$sdkRoot = Split-Path -Parent $PSScriptRoot
$oldPath = $env:PATH
$oldResolver = $env:MSBuildEnableWorkloadResolver
try {
    $env:MSBuildEnableWorkloadResolver = 'false'
    $nativeBuild = Join-Path $sdkRoot 'build/native/host'
    cmake -S (Join-Path $sdkRoot 'runtime') -B $nativeBuild
    if ($LASTEXITCODE -ne 0) { throw 'Native configure failed.' }
    cmake --build $nativeBuild --config Debug
    if ($LASTEXITCODE -ne 0) { throw 'Native build failed.' }
    ctest --test-dir $nativeBuild -C Debug --output-on-failure
    if ($LASTEXITCODE -ne 0) { throw 'Native tests failed.' }
    $env:PATH = "$nativeBuild;$nativeBuild/Debug;$oldPath"
    dotnet run --project (Join-Path $sdkRoot 'tests/CoreChecks/CoreChecks.csproj') -- --native
    if ($LASTEXITCODE -ne 0) { throw 'Core/native integration tests failed.' }
    if ($UnityManagedDir) {
        dotnet build (Join-Path $sdkRoot 'tests/UnityCompile/UnityCompile.csproj') "-p:UnityManagedDir=$UnityManagedDir" --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Unity reference compile failed.' }
    }
} finally {
    $env:PATH = $oldPath
    $env:MSBuildEnableWorkloadResolver = $oldResolver
}

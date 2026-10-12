param([string]$AndroidRepoPath = '')
$ErrorActionPreference = 'Stop'
$sdkRoot = Split-Path -Parent $PSScriptRoot
$lock = Get-Content -LiteralPath (Join-Path $sdkRoot 'third_party/spatial-ai-android.lock.json') -Raw | ConvertFrom-Json
$packageAar = Join-Path $sdkRoot $lock.artifact
$expectedHash = $lock.sha256.ToUpperInvariant()

if ($AndroidRepoPath) {
    if ($lock.sourceState -like 'local-working-tree-*') {
        throw 'This AAR was built from local Android changes. Commit and update the locked revision before restoring from a release source checkout.'
    }
    $androidRoot = (Resolve-Path -LiteralPath $AndroidRepoPath).Path
    $revision = (& git -C $androidRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $revision -ne $lock.revision) {
        throw "Android repository must be at locked revision $($lock.revision); got $revision"
    }
    $sourceAar = Join-Path $androidRoot 'build/artifacts/spatial-hand-unity.aar'
    if (!(Test-Path -LiteralPath $sourceAar)) { throw "Build the Android AAR first: $sourceAar" }
    $sourceHash = (Get-FileHash -LiteralPath $sourceAar -Algorithm SHA256).Hash
    if ($sourceHash -ne $expectedHash) { throw "Android AAR hash mismatch: $sourceHash" }
    if (!(Test-Path -LiteralPath $packageAar) -or
        (Get-FileHash -LiteralPath $packageAar -Algorithm SHA256).Hash -ne $expectedHash) {
        Copy-Item -LiteralPath $sourceAar -Destination $packageAar -Force
    }
}

if (!(Test-Path -LiteralPath $packageAar)) { throw "Packaged Android AAR missing: $packageAar" }
$packageHash = (Get-FileHash -LiteralPath $packageAar -Algorithm SHA256).Hash
if ($packageHash -ne $expectedHash) { throw "Packaged Android AAR hash mismatch: $packageHash" }
Write-Output "Android AAR verified: $packageHash"

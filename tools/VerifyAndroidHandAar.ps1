param([string]$AndroidRepoPath = '')
$ErrorActionPreference = 'Stop'
$sdkRoot = Split-Path -Parent $PSScriptRoot
$lock = Get-Content -LiteralPath (Join-Path $sdkRoot 'third_party/spatial-ai-android.lock.json') -Raw | ConvertFrom-Json
$packageAar = Join-Path $sdkRoot $lock.artifact
$expectedHash = $lock.sha256.ToUpperInvariant()

if (-not $AndroidRepoPath) {
    if ([IO.Path]::IsPathRooted($lock.repository) -or $lock.repository -match '://') {
        throw 'Android repository must be a local path relative to the SDK root.'
    }
    $AndroidRepoPath = Join-Path $sdkRoot $lock.repository
}
if ($AndroidRepoPath) {
    if ($lock.sourceState -like 'local-working-tree-*') {
        throw 'This AAR was built from local Android changes. Commit and update the locked revision before restoring from a release source checkout.'
    }
    $androidRoot = (Resolve-Path -LiteralPath $AndroidRepoPath).Path
    $gitRoot = (& git -C $androidRoot rev-parse --show-toplevel).Trim()
    if ($LASTEXITCODE -ne 0 -or [IO.Path]::GetFullPath($gitRoot).TrimEnd('\','/') -ne [IO.Path]::GetFullPath($androidRoot).TrimEnd('\','/')) {
        throw "Android repository path must point to its Git root: $androidRoot"
    }
    $revision = (& git -C $androidRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $revision -ne $lock.revision) {
        throw "Android repository must be at locked revision $($lock.revision); got $revision"
    }
    foreach ($entry in $lock.sourceFiles) {
        $source = Join-Path $androidRoot $entry.path
        if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw "Android source missing: $($entry.path)" }
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.sha256.ToUpperInvariant()) {
            $bytes = [IO.File]::ReadAllBytes($source)
            $normalized = New-Object System.IO.MemoryStream
            try {
                for ($i = 0; $i -lt $bytes.Length; $i++) {
                    if ($bytes[$i] -eq 13 -and $i + 1 -lt $bytes.Length -and $bytes[$i + 1] -eq 10) { continue }
                    $normalized.WriteByte($bytes[$i])
                }
                $hasher = [Security.Cryptography.SHA256]::Create()
                try { $hash = [BitConverter]::ToString($hasher.ComputeHash($normalized.ToArray())).Replace('-', '').ToLowerInvariant() }
                finally { $hasher.Dispose() }
            } finally { $normalized.Dispose() }
            if (-not $entry.normalizedSha256 -or $hash -ne $entry.normalizedSha256.ToLowerInvariant()) {
                throw "Android source checksum mismatch: $($entry.path)"
            }
        }
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

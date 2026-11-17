param(
    [string]$Workspace = 'D:/visor-x',
    [string]$Branch = 'main',
    [string]$ReleaseSdkName = 'spatial-ai-sdk',
    [string]$ReleaseAndroidName = 'spatial-ai-android',
    [switch]$SkipFetch
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath($Workspace)
$pairs = @(
    @{ Name='SDK'; Development='spatial-ai-sdk'; Release=(Join-Path 'release' $ReleaseSdkName) },
    @{ Name='Android'; Development='spatial-ai-android'; Release=(Join-Path 'release' $ReleaseAndroidName) }
)
function Invoke-CheckedGit([string]$directory, [string[]]$arguments) {
    $result = & git -C $directory @arguments
    if ($LASTEXITCODE -ne 0) { throw "Git failed in ${directory}: git $($arguments -join ' ')" }
    return ($result | Out-String).Trim()
}
foreach ($pair in $pairs) {
    $development = Join-Path $workspace $pair.Development
    $release = Join-Path $workspace $pair.Release
    if (-not $SkipFetch) {
        Invoke-CheckedGit $development @('fetch', 'origin') | Out-Null
        Invoke-CheckedGit $development @('fetch', 'github') | Out-Null
        Invoke-CheckedGit $release @('fetch', 'origin') | Out-Null
    }
    $origin = Invoke-CheckedGit $development @('rev-parse', "refs/remotes/origin/$Branch")
    $github = Invoke-CheckedGit $development @('rev-parse', "refs/remotes/github/$Branch")
    $releaseRemote = Invoke-CheckedGit $release @('rev-parse', "refs/remotes/origin/$Branch")
    if ($origin -ne $github -or $github -ne $releaseRemote) {
        throw "$($pair.Name): origin, github and release GitHub revisions differ: $origin / $github / $releaseRemote"
    }
    if ((Invoke-CheckedGit $development @('rev-parse', 'HEAD')) -ne $origin) {
        throw "$($pair.Name): development HEAD differs from synchronized $Branch."
    }
    $branchName = Invoke-CheckedGit $release @('branch', '--show-current')
    if ($branchName -ne $Branch) { throw "$($pair.Name): release checkout is on '$branchName', expected '$Branch'." }
    if (Invoke-CheckedGit $release @('status', '--porcelain', '--untracked-files=all')) {
        throw "$($pair.Name): release working tree is not clean; preserve or remove its changes before updating."
    }
    Invoke-CheckedGit $release @('merge', '--ff-only', "refs/remotes/origin/$Branch") | Out-Null
    if ((Invoke-CheckedGit $release @('rev-parse', 'HEAD')) -ne $github) { throw "$($pair.Name): release HEAD mismatch." }
    Write-Output "$($pair.Name): synchronized $github"
}
$sdkRelease = Join-Path (Join-Path $workspace 'release') $ReleaseSdkName
$androidRelease = Join-Path (Join-Path $workspace 'release') $ReleaseAndroidName
$lockPath = Join-Path $sdkRelease 'third_party/spatial-ai-android.lock.json'
$lock = Get-Content -LiteralPath $lockPath -Raw -Encoding UTF8 | ConvertFrom-Json
$androidHead = Invoke-CheckedGit $androidRelease @('rev-parse', 'HEAD')
if ($lock.revision -ne $androidHead) {
    throw "Android SDK artifact lock revision $($lock.revision) differs from release Android source $androidHead; rebuild and commit the SDK artifact lock before export."
}
$artifact = Join-Path $sdkRelease $lock.artifact
if (-not (Test-Path -LiteralPath $artifact -PathType Leaf)) { throw "Locked Android AAR is missing: $artifact" }
if ((Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant() -ne $lock.sha256.ToLowerInvariant()) {
    throw 'Android AAR SHA-256 does not match the committed lock.'
}
foreach ($entry in $lock.sourceFiles) {
    $source = Join-Path $androidRelease $entry.path
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Android source missing: $($entry.path)" }
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256.ToLowerInvariant()) {
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
        if (-not $entry.normalizedSha256 -or $hash -ne $entry.normalizedSha256.ToLowerInvariant()) { throw "Android source checksum mismatch: $($entry.path)" }
    }
}
Write-Output 'Release source and Android AAR lock checks passed.'

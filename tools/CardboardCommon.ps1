$ErrorActionPreference = 'Stop'

function Read-CardboardLock {
    param([string]$Root)
    $lock = Get-Content -LiteralPath (Join-Path $Root 'third_party/cardboard.lock.json') -Raw | ConvertFrom-Json
    if ($lock.schemaVersion -ne 1) { throw 'Unsupported upstream lock schema.' }
    return $lock
}

function Assert-CardboardCheckout {
    param([string]$Path, $Entry)
    if (!(Test-Path -LiteralPath (Join-Path $Path '.git'))) { throw "Missing checkout: $Path. Run FetchCardboard.ps1." }
    $head = & git -C $Path rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or "$head".Trim() -ne $Entry.commit) { throw "Unexpected commit at $Path. Expected $($Entry.commit)." }
    $origin = & git -C $Path remote get-url origin
    if ($LASTEXITCODE -ne 0 -or "$origin".Trim() -ne $Entry.url) { throw "Unexpected upstream origin at $Path." }
    $dirty = & git -C $Path status --porcelain --untracked-files=all
    if ($LASTEXITCODE -ne 0) { throw "Cannot inspect checkout: $Path" }
    if ($dirty) { throw "Upstream has local changes: $Path. Keep adapter changes outside the pinned checkout." }
}

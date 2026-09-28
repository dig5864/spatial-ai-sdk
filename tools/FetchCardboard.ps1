param([switch]$VerifyOnly)
. (Join-Path $PSScriptRoot 'CardboardCommon.ps1')
$sdkRoot = Split-Path -Parent $PSScriptRoot
$lock = Read-CardboardLock $sdkRoot
$checkoutRoot = Join-Path $sdkRoot 'third_party/checkouts'
foreach ($entry in $lock.repositories) {
    $path = Join-Path $checkoutRoot $entry.directory
    if (!(Test-Path -LiteralPath $path)) {
        if ($VerifyOnly) { throw "Missing checkout: $path" }
        New-Item -ItemType Directory -Path $checkoutRoot -Force | Out-Null
        & git -c http.version=HTTP/1.1 -c http.lowSpeedLimit=1024 -c http.lowSpeedTime=45 clone --depth 1 --filter=blob:none --sparse --branch $entry.tag $entry.url $path
        if ($LASTEXITCODE -ne 0) { throw "Clone failed: $($entry.id). No existing files were removed." }
    }
    Assert-CardboardCheckout $path $entry
    if (!$VerifyOnly) {
        & git -C $path -c http.version=HTTP/1.1 sparse-checkout set --no-cone @($entry.sparsePatterns)
        if ($LASTEXITCODE -ne 0) { throw "Sparse checkout failed: $($entry.id)" }
        Assert-CardboardCheckout $path $entry
    }
    Write-Output "$($entry.id): $($entry.commit) verified"
}

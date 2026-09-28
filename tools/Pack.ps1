$ErrorActionPreference = 'Stop'
$sdkRoot = Split-Path -Parent $PSScriptRoot
$packageRoot = Join-Path $sdkRoot 'packages/com.spatialai.sdk'
$manifest = Get-Content -LiteralPath (Join-Path $packageRoot 'package.json') -Raw | ConvertFrom-Json
if ($manifest.name -ne 'com.spatialai.sdk' -or $manifest.version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw 'Invalid Spatial AI UPM package name or version.'
}
$fileName = "$($manifest.name)-$($manifest.version).tgz"
$releaseDir = Join-Path $sdkRoot "dist/$($manifest.version)/unity"
$destination = Join-Path $releaseDir $fileName
if (Test-Path -LiteralPath $destination) { throw "Release package already exists: $destination. Bump the version before publishing changed contents." }
# Stable GUIDs make sample scripts and package assets safe to share in source control.
$sha = [System.Security.Cryptography.SHA256]::Create()
try {
    foreach ($item in Get-ChildItem -LiteralPath $packageRoot -Recurse | Where-Object { $_.Extension -ne '.meta' }) {
        $relative = $item.FullName.Substring($packageRoot.Length).Replace('\','/')
        $hash = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes('com.spatialai.sdk' + $relative))
        $guid = ([System.BitConverter]::ToString($hash).Replace('-','').ToLowerInvariant()).Substring(0,32)
        $metaPath = $item.FullName + '.meta'
        if (Test-Path -LiteralPath $metaPath) { continue }
        $lines = @('fileFormatVersion: 2', "guid: $guid")
        if ($item.PSIsContainer) { $lines += 'folderAsset: yes'; $importer = 'DefaultImporter' }
        elseif ($item.Extension -eq '.cs') { $importer = 'MonoImporter' }
        elseif ($item.Extension -eq '.asmdef') { $importer = 'AssemblyDefinitionImporter' }
        elseif ($item.Extension -eq '.shader') { $importer = 'ShaderImporter' }
        elseif ($item.Name -eq 'package.json') { $importer = 'PackageManifestImporter' }
        else { $importer = 'DefaultImporter' }
        $lines += @("${importer}:", '  externalObjects: {}', '  userData: ', '  assetBundleName: ', '  assetBundleVariant: ')
        [System.IO.File]::WriteAllLines($metaPath, $lines, [System.Text.UTF8Encoding]::new($false))
    }
} finally { $sha.Dispose() }
$stage = Join-Path $sdkRoot ('build/staging/unity/package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -LiteralPath $packageRoot -Destination (Join-Path $stage 'package') -Recurse
$archive = Join-Path $stage $fileName
tar -czf $archive -C $stage package
if ($LASTEXITCODE -ne 0) { throw 'Package archive failed.' }
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
Copy-Item -LiteralPath $archive -Destination $destination
$checksum = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $releaseDir 'SHA256SUMS.txt'), "$checksum  $fileName`n", [Text.UTF8Encoding]::new($false))
$releaseManifest = [ordered]@{
    schemaVersion = 1
    package = $manifest.name
    version = $manifest.version
    platform = 'unity'
    file = $fileName
    sha256 = $checksum
    status = 'local-candidate-not-published'
}
[IO.File]::WriteAllText((Join-Path $releaseDir 'release-manifest.json'), ($releaseManifest | ConvertTo-Json -Depth 3), [Text.UTF8Encoding]::new($false))
Write-Output $destination

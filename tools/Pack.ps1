param([string]$OutputDirectory = '', [switch]$ReplaceLocalCandidate)
$ErrorActionPreference = 'Stop'
$sdkRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'VerifyAndroidHandAar.ps1')
$arguments = @((Join-Path $PSScriptRoot 'PackUnityRelease.py'))
if ($OutputDirectory) { $arguments += @('--output', [IO.Path]::GetFullPath($OutputDirectory)) }
if ($ReplaceLocalCandidate) { $arguments += '--replace-local-candidate' }
& python @arguments
if ($LASTEXITCODE -ne 0) { throw 'Unity release packaging failed.' }

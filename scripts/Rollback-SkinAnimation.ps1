param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue){throw 'Close the game normally before rollback.'}
$mods=[IO.Path]::GetFullPath((Join-Path $GamePath 'mods'))
$source=[IO.Path]::GetFullPath((Join-Path $mods 'SkinAnimationBridge'))
if(-not $source.StartsWith($mods.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected module path.'}
if(-not(Test-Path -LiteralPath $source)){Write-Output 'SkinAnimationBridge is not installed. No change required.';return}
$archive=Join-Path $root ('artifacts/skin-animation-rollback-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $archive|Out-Null
Move-Item -LiteralPath $source -Destination (Join-Path $archive 'SkinAnimationBridge')
Write-Output "Bridge removed and preserved in: $archive"
Write-Output 'Author files, settings and saved runs were not modified.'

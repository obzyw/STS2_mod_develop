param([Parameter(Mandatory=$true)][string]$GamePath,[switch]$ApplyVisualProfile,[switch]$MatchDisplay)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$GamePath=[IO.Path]::GetFullPath($GamePath)
if(Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue){throw 'Close the game first. Do not abandon the run.'}
if(-not(Test-Path -LiteralPath (Join-Path $GamePath 'SlayTheSpire2.exe'))){throw 'Game path is invalid.'}
$release=Get-Content -LiteralPath (Join-Path $GamePath 'release_info.json') -Raw | ConvertFrom-Json
if($release.version -ne 'v0.111.0'){throw 'This tested snapshot requires game v0.111.0; re-audit a newer version instead of forcing it.'}
$workshop=Join-Path (Split-Path (Split-Path $GamePath -Parent) -Parent) 'workshop/content/2868840'
$gold=Join-Path $workshop '3821345574/NecrobinderVfx.dll'
if(-not(Test-Path -LiteralPath $gold)){throw 'Subscribe to the required author mods listed in README first.'}
$original='1C68A4699FDC0BB25BC0737FE811E9A368442A47D922F9A6B3F493DED28433AA'
$patched='88C0BA2ABB78D8EEA9A4983972449EE3E73A3AB96B8DEE14261DA8F1F8EE1647'
$goldHash=(Get-FileHash -LiteralPath $gold).Hash
if($goldHash -notin @($original,$patched)){throw 'The gold mod was updated; this snapshot must be adapted before installation.'}
foreach($item in (Get-Content -LiteralPath (Join-Path $projectRoot 'portable/files.json') -Raw | ConvertFrom-Json)){
    if((Get-FileHash -LiteralPath (Join-Path $projectRoot $item.path)).Hash -ne $item.sha256){throw 'Portable payload integrity check failed.'}
}
if($goldHash -eq $original){
    & (Join-Path $PSScriptRoot 'Build-NecroCompat.ps1') -InputDll $gold -GamePath $GamePath -CecilPath (Join-Path $projectRoot 'portable/Mono.Cecil.dll')
    & (Join-Path $PSScriptRoot 'Deploy-NecroCompat.ps1') -GamePath $GamePath
}
$snapshot=Join-Path $projectRoot ('artifacts/portable-install-'+(Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Path $snapshot -Force | Out-Null
foreach($source in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'portable/mods') -Directory){
    $target=Join-Path $GamePath ('mods/'+$source.Name)
    if(Test-Path -LiteralPath $target){
        $resolved=[IO.Path]::GetFullPath($target)
        if(-not $resolved.StartsWith($GamePath+'\mods\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected mod archive path.'}
        Move-Item -LiteralPath $target -Destination (Join-Path $snapshot $source.Name)
    }
    Copy-Item -LiteralPath $source.FullName -Destination $target -Recurse
}
if($ApplyVisualProfile){
    $index=Join-Path $projectRoot 'profiles/author-visual-settings/index.json'
    foreach($setting in (Get-Content -LiteralPath $index -Raw | ConvertFrom-Json)){
        $userRoot=if($setting.root -eq 'appdata'){Join-Path $env:APPDATA 'SlayTheSpire2'}else{Join-Path $env:LOCALAPPDATA 'SlayTheSpire2'}
        $target=Join-Path $userRoot $setting.relativePath
        if(Test-Path -LiteralPath $target){Copy-Item -LiteralPath $target -Destination (Join-Path $snapshot $setting.file)}
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path (Split-Path $index -Parent) $setting.file) -Destination $target -Force
    }
}
if($MatchDisplay){& (Join-Path $PSScriptRoot 'Set-DisplayStability.ps1')}
Write-Output "Tested snapshot installed. Existing local files backed up to: $snapshot"

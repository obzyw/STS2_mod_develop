param([Parameter(Mandatory=$true)][string]$GamePath,[switch]$CheckOnly)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$GamePath=[IO.Path]::GetFullPath($GamePath)
if(-not(Test-Path -LiteralPath (Join-Path $GamePath 'SlayTheSpire2.exe'))){throw 'Supply the game installation directory.'}
$settings=Join-Path $env:APPDATA 'SlayTheSpire2/mod_settings/MoreSilentAnimations.json'
$required=@('SilentMovementCompat','SilentEffectsCompat')
if(-not $CheckOnly){
    if(Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue){throw 'Close the game normally first; do not abandon the run.'}
    & (Join-Path $PSScriptRoot 'Deploy-SilentCompat.ps1') -GamePath $GamePath
    & (Join-Path $PSScriptRoot 'Deploy-SilentEffectsCompat.ps1') -GamePath $GamePath
    if(Test-Path -LiteralPath (Join-Path $GamePath 'mods/KnifeTrapStages')){
        & (Join-Path $PSScriptRoot 'Deploy-KnifeTrapStages.ps1') -GamePath $GamePath -Rollback
    }
    $backup=Join-Path $root ('artifacts/silent-setup-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Force -Path $backup|Out-Null
    if(Test-Path -LiteralPath $settings){Copy-Item -LiteralPath $settings -Destination (Join-Path $backup 'MoreSilentAnimations.json')}
    New-Item -ItemType Directory -Force -Path (Split-Path $settings -Parent)|Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'profiles/silent-setup.json') -Destination $settings -Force
    Write-Output "Previous visual settings preserved: $backup"
}
$missing=@($required|Where-Object{-not(Test-Path -LiteralPath (Join-Path $GamePath "mods/$_/$_.dll"))})
$current=if(Test-Path -LiteralPath $settings){Get-Content -LiteralPath $settings -Raw|ConvertFrom-Json}else{$null}
$status=[ordered]@{Installed=@($required|Where-Object{$_ -notin $missing});Missing=$missing;SettingsFile=$settings;AnimationEnabled=$(if($current){$current.Enabled}else{'author-default'});DisabledCards=$(if($current){@($current.DisabledCards)}else{@()})}
$status|ConvertTo-Json -Depth 4
if($missing.Count){throw 'Silent compatibility is incomplete. Run this script without -CheckOnly.'}
foreach($name in $required){
    $localManifest=Join-Path $GamePath "mods/$name/$name.json"
    $sourceManifest=if($name -eq 'SilentMovementCompat'){Join-Path $root 'compatibility/SilentMovementCompat.json'}else{Join-Path $root 'effects/SilentEffectsCompat.json'}
    if(-not(Test-Path -LiteralPath $localManifest) -or (Get-Content -LiteralPath $localManifest -Raw|ConvertFrom-Json).version -ne (Get-Content -LiteralPath $sourceManifest -Raw|ConvertFrom-Json).version){throw "Outdated local compatibility: $name. Run without -CheckOnly."}
}
if($current -and -not $current.Enabled){throw 'The author animation master switch is off.'}
Write-Output 'Silent setup present. Author updates still require version compatibility checks.'

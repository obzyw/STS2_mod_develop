param([string]$GamePath,[string]$Profile,[string]$RollbackFrom)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if(-not $GamePath){$GamePath=Split-Path $projectRoot -Parent}
$GamePath=[IO.Path]::GetFullPath($GamePath)
if(Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue){throw 'Close the game first.'}
if(-not(Test-Path -LiteralPath (Join-Path $GamePath 'SlayTheSpire2.exe'))){throw 'Supply -GamePath.'}
$target=Join-Path $GamePath 'mods/STS2UnifiedCompat'
$config=Join-Path $env:APPDATA 'SlayTheSpire2/ModConfig/STS2UnifiedCompat.json'
$gold=Join-Path (Split-Path (Split-Path $GamePath -Parent) -Parent) 'workshop/content/2868840/3821345574/NecrobinderVfx.dll'
if($RollbackFrom){
    $record=Get-Content -LiteralPath $RollbackFrom -Raw | ConvertFrom-Json
    $backupRoot=Split-Path ([IO.Path]::GetFullPath($RollbackFrom)) -Parent
    if(Test-Path -LiteralPath $target){Move-Item -LiteralPath $target -Destination (Join-Path $backupRoot ('unified-removed-'+(Get-Date -Format yyyyMMdd-HHmmss)))}
    foreach($name in $record.archivedMods){
        $previous=Join-Path $backupRoot ('mods/'+$name)
        if(Test-Path -LiteralPath (Join-Path $GamePath ('mods/'+$name))){throw "Existing mod would be overwritten: $name"}
        Move-Item -LiteralPath $previous -Destination (Join-Path $GamePath ('mods/'+$name))
    }
    if($record.goldRestored){Copy-Item -LiteralPath (Join-Path $backupRoot 'gold.before.dll') -Destination $gold -Force}
    if($record.configExisted){Copy-Item -LiteralPath (Join-Path $backupRoot 'config.before.json') -Destination $config -Force}
    elseif(Test-Path -LiteralPath $config){Move-Item -LiteralPath $config -Destination (Join-Path $backupRoot ('config.removed-'+(Get-Date -Format yyyyMMdd-HHmmss)+'.json'))}
    Write-Output 'Previous local installation restored.'
    exit 0
}
if(Test-Path -LiteralPath $target){throw 'Unified mod already installed; use its rollback record before replacing it.'}
if($Profile){$null=Get-Content -LiteralPath $Profile -Raw | ConvertFrom-Json}
& (Join-Path $PSScriptRoot 'Build-Unified.ps1') -GamePath $GamePath
if($LASTEXITCODE -ne 0){throw 'Build failed.'}
$output=Join-Path $projectRoot ('artifacts/unified-deployment-'+(Get-Date -Format yyyyMMdd-HHmmss))
New-Item -ItemType Directory -Path (Join-Path $output 'mods') -Force | Out-Null
$record=@{archivedMods=@();goldRestored=$false;configExisted=(Test-Path -LiteralPath $config);profileApplied=[bool]$Profile}
if($record.configExisted){Copy-Item -LiteralPath $config -Destination (Join-Path $output 'config.before.json')}
$legacy=@('SilentMovementCompat','SilentEffectsCompat','WatcherDivinityPose','WatcherStanceTransitions')
foreach($name in $legacy){
    $existing=[IO.Path]::GetFullPath((Join-Path $GamePath ('mods/'+$name)))
    if(-not $existing.StartsWith($GamePath+'\mods\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unexpected mod archive path.'}
    if(Test-Path -LiteralPath $existing){
        Move-Item -LiteralPath $existing -Destination (Join-Path $output ('mods/'+$name))
        $record.archivedMods+= $name
    }
}
if((Test-Path -LiteralPath $gold) -and (Get-FileHash -LiteralPath $gold).Hash -eq '88C0BA2ABB78D8EEA9A4983972449EE3E73A3AB96B8DEE14261DA8F1F8EE1647'){
    $original=Join-Path $projectRoot 'artifacts/backups/1C68A4699FDC0BB25BC0737FE811E9A368442A47D922F9A6B3F493DED28433AA/NecrobinderVfx.dll'
    if(-not(Test-Path -LiteralPath $original) -or (Get-FileHash -LiteralPath $original).Hash -ne '1C68A4699FDC0BB25BC0737FE811E9A368442A47D922F9A6B3F493DED28433AA'){throw 'Restore the original gold DLL through Steam or provide the local legacy backup before migration.'}
    Copy-Item -LiteralPath $gold -Destination (Join-Path $output 'gold.before.dll')
    Copy-Item -LiteralPath $original -Destination $gold -Force
    $record.goldRestored=$true
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'artifacts/packages/STS2UnifiedCompat') -Destination $target -Recurse
if($Profile){
    New-Item -ItemType Directory -Path (Split-Path $config -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $Profile -Destination $config -Force
}
$record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'report.json') -Encoding UTF8
Write-Output "Unified mod installed. Rollback: $output/report.json"

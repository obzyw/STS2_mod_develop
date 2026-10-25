param([string]$SettingsPath,[switch]$AllowRuntime,[string]$RollbackFrom)
$ErrorActionPreference='Stop'
if(Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue | Where-Object {-not $_.HasExited}){throw 'Close the game before changing donor isolation.'}
$workbench=Split-Path $PSScriptRoot -Parent
$projectRoot=Split-Path $workbench -Parent
if(-not $SettingsPath){
    $accountRoot=Join-Path $env:APPDATA 'SlayTheSpire2/steam'
    $candidates=@(Get-ChildItem -LiteralPath $accountRoot -Directory | ForEach-Object {Join-Path $_.FullName 'settings.save'} | Where-Object {Test-Path -LiteralPath $_})
    if($candidates.Count -ne 1){throw 'Specify -SettingsPath for the intended Steam account.'}
    $SettingsPath=$candidates[0]
}
$SettingsPath=[IO.Path]::GetFullPath($SettingsPath)
$cfg=Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json
if(-not $cfg.mod_settings -or -not $cfg.mod_settings.mod_list){throw 'Unexpected native mod-list schema.'}
$id='WatcherReimagined'
$entries=@($cfg.mod_settings.mod_list | Where-Object id -eq $id)
if($entries.Count -gt 1){throw 'Duplicate donor entries; inspect before changing.'}
if($RollbackFrom){
    $record=Get-Content -LiteralPath $RollbackFrom -Raw | ConvertFrom-Json
    if($record.account -ne (Split-Path (Split-Path $SettingsPath -Parent) -Leaf)){throw 'Rollback belongs to a different account.'}
    if((Get-FileHash -LiteralPath $SettingsPath).Hash -eq $record.afterHash){
        Copy-Item -LiteralPath (Join-Path (Split-Path $RollbackFrom -Parent) 'native-settings-before.save') -Destination $SettingsPath -Force
    }else{
        $cfg.mod_settings.mod_list=@($cfg.mod_settings.mod_list | Where-Object id -ne $id)
        if($record.existed){$cfg.mod_settings.mod_list+=@($record.before)}
        [IO.File]::WriteAllText($SettingsPath,($cfg | ConvertTo-Json -Depth 40),[Text.UTF8Encoding]::new($false))
    }
    Write-Output 'Previous donor enablement restored; other current preferences preserved.'
    exit 0
}
$before=$(if($entries.Count){$entries[0] | ConvertTo-Json | ConvertFrom-Json}else{$null})
if($entries.Count){$entries[0].is_enabled=[bool]$AllowRuntime}
else{$cfg.mod_settings.mod_list+=@([pscustomobject]@{id=$id;is_enabled=[bool]$AllowRuntime;source='steam_workshop'})}
$output=Join-Path $projectRoot ('artifacts/watcher-isolation-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $output | Out-Null
Copy-Item -LiteralPath $SettingsPath -Destination (Join-Path $output 'native-settings-before.save')
[IO.File]::WriteAllText($SettingsPath,($cfg | ConvertTo-Json -Depth 40),[Text.UTF8Encoding]::new($false))
@{account=(Split-Path (Split-Path $SettingsPath -Parent) -Leaf);existed=($entries.Count -gt 0);before=$before;enabled=[bool]$AllowRuntime;afterHash=(Get-FileHash -LiteralPath $SettingsPath).Hash} |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'report.json') -Encoding UTF8
Write-Output ("Donor enabled="+[bool]$AllowRuntime+"; rollback record: $output/report.json")

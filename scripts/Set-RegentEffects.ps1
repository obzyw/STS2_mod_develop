param([string]$GamePath,[string]$RollbackFrom)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if(-not $GamePath){$GamePath=Split-Path $projectRoot -Parent}
if(Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue | Where-Object {-not $_.HasExited}){throw 'Close the game before changing animation preferences.'}
$files=@{
    RegentFX=(Join-Path $env:LOCALAPPDATA 'SlayTheSpire2/RegentFX/ritsu_interop_state.json')
    RegentAnimations=(Join-Path $env:APPDATA 'SlayTheSpire2/mod_settings/RegentAnimations.json')
}
if($RollbackFrom){
    $report=Get-Content -LiteralPath $RollbackFrom -Raw | ConvertFrom-Json
    $backupRoot=Split-Path ([IO.Path]::GetFullPath($RollbackFrom)) -Parent
    foreach($entry in $report.files){
        $path=$files[$entry.id]
        if(-not $path -or -not(Test-Path -LiteralPath $path)){throw 'Expected author configuration is missing.'}
        if((Get-FileHash -LiteralPath $path).Hash -eq $entry.afterHash){
            Copy-Item -LiteralPath (Join-Path $backupRoot ($entry.id+'.before.json')) -Destination $path -Force
        }else{
            # Preserve later, unrelated user preferences. Restore only our
            # changed keys that still hold the value this configuration applied.
            $values=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            foreach($change in $entry.changes){
                $property=$values.PSObject.Properties[$change.key]
                if($property -and $property.Value -eq $change.after){
                    if($change.existed){$property.Value=$change.before}else{$values.PSObject.Properties.Remove($change.key)}
                }
            }
            [IO.File]::WriteAllText($path,($values | ConvertTo-Json -Depth 20),[Text.UTF8Encoding]::new($false))
        }
    }
    Write-Output 'Regent animation preferences restored; later unrelated settings preserved.'
    exit 0
}
$GamePath=[IO.Path]::GetFullPath($GamePath)
$hashes=@{
    'data_sts2_windows_x86_64/sts2.dll'='0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9'
    '../../workshop/content/2868840/3747497501/RegentFX.dll'='0F0262B3C4304C824C81D908CA7F3CEDABD00AF4C6B522A02513FF380C36EF06'
    '../../workshop/content/2868840/3820682341/RegentAnimations.dll'='389CEF1F20B66499E04C0CE12D9F33D3A8FB1A4DB9A158DEB45FE279D8BC31A2'
}
foreach($relative in $hashes.Keys){if((Get-FileHash -LiteralPath (Join-Path $GamePath $relative)).Hash -ne $hashes[$relative]){throw "Re-audit changed binary: $relative"}}
$attacks=@('Comet','CrescentSpear','DyingStar','SevenStars','AstralPulse')
$powers=@('BlackHole','Genesis','PillarOfCreation','SpectrumShift')
$desired=@{RegentFX=@{};RegentAnimations=@{}}
foreach($name in $attacks){$desired.RegentFX['card_'+$name]=$true;$desired.RegentAnimations[$name]=$false}
foreach($name in $powers){$desired.RegentFX['power_'+$name]=$true;$desired.RegentAnimations[$name]=$true}
$prepared=@()
foreach($id in $files.Keys){
    $path=$files[$id]
    if(-not(Test-Path -LiteralPath $path)){throw "Initialize the author settings first: $id"}
    $raw=[IO.File]::ReadAllText($path);$values=$raw | ConvertFrom-Json;$changes=@()
    foreach($key in $desired[$id].Keys){
        $property=$values.PSObject.Properties[$key];$existed=$null -ne $property
        # Missing card/power keys use the authors' enabled default.
        $before=$(if($existed){[bool]$property.Value}else{$true})
        $after=[bool]$desired[$id][$key]
        if($before -eq $after){continue}
        $changes+=@{key=$key;existed=$existed;before=$before;after=$after}
        if($existed){$property.Value=$after;$raw=[regex]::Replace($raw,('("'+$key+'"\s*:\s*)(true|false)'),('${1}'+$after.ToString().ToLowerInvariant()))}
        else{$values | Add-Member -NotePropertyName $key -NotePropertyValue $after;$raw=$values | ConvertTo-Json -Depth 20}
    }
    $parsed=$raw | ConvertFrom-Json
    foreach($change in $changes){if($parsed.($change.key) -ne $change.after){throw 'Settings schema mismatch; no preferences written.'}}
    $prepared+=@{id=$id;path=$path;raw=$raw;changes=$changes}
}
$output=Join-Path $projectRoot ('artifacts/regent-settings-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $output | Out-Null
$records=@()
foreach($entry in $prepared){
    $backup=Join-Path $output ($entry.id+'.before.json')
    Copy-Item -LiteralPath $entry.path -Destination $backup
    $records+=@{id=$entry.id;beforeHash=(Get-FileHash -LiteralPath $backup).Hash;changes=$entry.changes}
}
try{
    foreach($entry in $prepared){[IO.File]::WriteAllText($entry.path,$entry.raw,[Text.UTF8Encoding]::new($false))}
}catch{
    foreach($entry in $prepared){Copy-Item -LiteralPath (Join-Path $output ($entry.id+'.before.json')) -Destination $entry.path -Force}
    throw
}
foreach($record in $records){$record.afterHash=(Get-FileHash -LiteralPath $files[$record.id]).Hash}
@{state='APPLIED';approach='author-settings-only';files=$records} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'report.json') -Encoding UTF8
Write-Output "Regent author settings applied; rollback: $output/report.json"

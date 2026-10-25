param([string]$SettingsPath,[int]$FpsLimit=0,[string]$RollbackFrom)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$userRoot=Join-Path $env:APPDATA 'SlayTheSpire2'
if(Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue){throw 'Close the game before changing display preferences.'}
if($RollbackFrom){
    $report=Get-Content -LiteralPath $RollbackFrom -Raw | ConvertFrom-Json
    if(-not $SettingsPath){$SettingsPath=Join-Path $userRoot $report.settingsRelativePath}
}elseif(-not $SettingsPath){
    $candidates=@(Get-ChildItem -LiteralPath (Join-Path $userRoot 'steam') -Directory | ForEach-Object {
        $candidate=Join-Path $_.FullName 'settings.save'
        if(Test-Path -LiteralPath $candidate){$candidate}
    })
    if($candidates.Count -ne 1){throw 'Specify -SettingsPath for the active Steam account settings.save.'}
    $SettingsPath=$candidates[0]
}
$SettingsPath=[IO.Path]::GetFullPath($SettingsPath)
$raw=[IO.File]::ReadAllText($SettingsPath)
$settings=$raw | ConvertFrom-Json
if($RollbackFrom -and (Get-FileHash -LiteralPath $SettingsPath).Hash -eq $report.afterHash){
    Copy-Item -LiteralPath (Join-Path (Split-Path $RollbackFrom -Parent) 'settings.before.json') -Destination $SettingsPath -Force
    Write-Output 'Original display preferences restored exactly.'
    exit 0
}
if(-not $RollbackFrom){
    if($FpsLimit -eq 0){
        $rates=@(Get-CimInstance Win32_VideoController | Where-Object {$_.CurrentRefreshRate -ge 30 -and $_.CurrentRefreshRate -le 500} | Select-Object -ExpandProperty CurrentRefreshRate -Unique)
        if($rates.Count -ne 1){throw 'Specify -FpsLimit using the game display refresh rate.'}
        $FpsLimit=[int]$rates[0]
    }
    if($FpsLimit -lt 30 -or $FpsLimit -gt 500){throw 'FPS limit must be between 30 and 500.'}
    $desired=@{fps_limit=$FpsLimit;vsync='on'}
}else{
    $desired=@{}
    foreach($change in $report.changes){
        # A later user adjustment takes precedence; restore only values still owned by this change.
        if($settings.($change.key) -eq $change.after){$desired[$change.key]=$change.before}
    }
}
$changes=@()
foreach($key in $desired.Keys){
    if($null -eq $settings.PSObject.Properties[$key]){throw "Missing original display preference: $key"}
    $before=$settings.($key);$after=$desired[$key]
    if($before -eq $after){continue}
    $pattern='("'+$key+'"\s*:\s*)("[^"]*"|[0-9]+)'
    if([regex]::Matches($raw,$pattern).Count -ne 1){throw "Ambiguous preference: $key"}
    $literal=$after | ConvertTo-Json -Compress
    $raw=[regex]::Replace($raw,$pattern,('${1}'+$literal))
    $changes+=@{key=$key;before=$before;after=$after}
}
$parsed=$raw | ConvertFrom-Json
foreach($change in $changes){if($parsed.($change.key) -ne $change.after){throw 'Display preference validation failed.'}}
if(-not $changes.Count){Write-Output 'Display preferences already match; no files changed.';exit 0}
if(-not $RollbackFrom){
    $output=Join-Path $projectRoot ('artifacts/display-settings-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
    New-Item -ItemType Directory -Path $output | Out-Null
    Copy-Item -LiteralPath $SettingsPath -Destination (Join-Path $output 'settings.before.json')
    $beforeHash=(Get-FileHash -LiteralPath $SettingsPath).Hash
}
[IO.File]::WriteAllText($SettingsPath,$raw,[Text.UTF8Encoding]::new($false))
if(-not $RollbackFrom){
    $relative=$SettingsPath.Substring(([IO.Path]::GetFullPath($userRoot)).Length).TrimStart('\','/')
    @{settingsRelativePath=$relative;beforeHash=$beforeHash;afterHash=(Get-FileHash -LiteralPath $SettingsPath).Hash;changes=$changes} |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'report.json') -Encoding UTF8
    Write-Output "Display preferences applied; rollback record: $output/report.json"
}else{Write-Output 'Display preferences restored; unrelated later preferences preserved.'}

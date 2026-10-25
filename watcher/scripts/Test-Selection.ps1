param([string]$GamePath,[int]$TimeoutSeconds=180,[switch]$DisplayObservation)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(-not $GamePath){$GamePath=Split-Path $projectRoot -Parent}
if(Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue){throw 'Close the game before the menu test.'}
$probe=Join-Path $GamePath 'mods/STS2CompatProbe'
if(Test-Path -LiteralPath $probe){throw 'Temporary probe already exists; refusing to overwrite.'}
$output=Join-Path $projectRoot ('artifacts/watcher-menu-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $output | Out-Null
$userRoot=Join-Path $env:APPDATA 'SlayTheSpire2'
$snapshots=@()
$variables=@{SteamAppId='2868840';SteamGameId='2868840';STS2_WATCHER_MENU_PROBE='1';STS2_WATCHER_MENU_OUTPUT=$output}
if($DisplayObservation){$variables.STS2_DISPLAY_OBSERVATION='1'}
$savedEnvironment=@{}
$process=$null
$created=$false
try{
    $sources=@(Get-ChildItem -LiteralPath (Join-Path $userRoot 'steam') -Recurse -File | Where-Object {$_.Name -in @('settings.save','prefs.save')})
    foreach($file in $sources){
        $relative=$file.FullName.Substring($userRoot.Length+1)
        $backup=Join-Path $output ('before/'+$relative)
        New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $backup
        $snapshots+=@{source=$file.FullName;backup=$backup;hash=(Get-FileHash -LiteralPath $file.FullName).Hash}
    }
    $settings=Get-Content -LiteralPath (Join-Path $userRoot 'WatcherReimagined/settings.json') -Raw | ConvertFrom-Json
    if($settings.motion -or $settings.card_art -or -not $settings.effects){throw 'Configure author effects-only switches before testing.'}
    & dotnet build (Join-Path $projectRoot 'verification/NativeProbe.csproj') -c Release -o (Join-Path $projectRoot 'artifacts/watcher-menu-probe-build') "-p:GamePath=$GamePath" --nologo
    if($LASTEXITCODE -ne 0){throw 'Probe build failed.'}
    New-Item -ItemType Directory -Path $probe | Out-Null
    $created=$true
    Copy-Item -LiteralPath (Join-Path $projectRoot 'artifacts/watcher-menu-probe-build/STS2CompatProbe.dll') -Destination $probe
    Copy-Item -LiteralPath (Join-Path $projectRoot 'verification/NativeProbe.json') -Destination (Join-Path $probe 'STS2CompatProbe.json')
    foreach($key in $variables.Keys){
        $savedEnvironment[$key]=[Environment]::GetEnvironmentVariable($key,'Process')
        [Environment]::SetEnvironmentVariable($key,$variables[$key],'Process')
    }
    $process=Start-Process -FilePath (Join-Path $GamePath 'SlayTheSpire2.exe') -WorkingDirectory $GamePath -ArgumentList "--log-file `"$(Join-Path $output 'game.log')`"" -WindowStyle Hidden -PassThru
    # Retain the native handle before exit; otherwise PowerShell may lose ExitCode for a child process.
    $null=$process.Handle
    $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while(-not $process.HasExited -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Seconds 1}
    if(-not $process.HasExited){throw "Menu test timeout: $output"}
    $process.WaitForExit()
    @{exitCode=$process.ExitCode;hasExited=$process.HasExited} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'process-result.json') -Encoding UTF8
    $result=Get-Content -LiteralPath (Join-Path $output 'menu-result.json') -Raw | ConvertFrom-Json
    if(-not $result.success -or $process.ExitCode -ne 0){throw "Menu assertion failed: $output"}
    Write-Output "PASS: $output"
}finally{
    if($process){
        if(-not $process.HasExited){Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue}
        $process.WaitForExit()
    }
    # Restore preferences before cleanup so a transient DLL lock cannot skip restoration.
    $restored=@()
    foreach($item in $snapshots){
        Copy-Item -LiteralPath $item.backup -Destination $item.source -Force
        $ok=(Get-FileHash -LiteralPath $item.source).Hash -eq $item.hash
        $restored+=@{source=$item.source;hash=$item.hash;restored=$ok}
    }
    $restored | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'restored.json') -Encoding UTF8
    foreach($key in $savedEnvironment.Keys){[Environment]::SetEnvironmentVariable($key,$savedEnvironment[$key],'Process')}
    if($created){
        # Keep the exact test build as evidence outside the game's mod search directory.
        $archive=Join-Path $output 'probe-not-deployed'
        for($attempt=0;$attempt -lt 5;$attempt++){
            try{Move-Item -LiteralPath $probe -Destination $archive;break}
            catch{if($attempt -eq 4){throw};Start-Sleep -Milliseconds 500}
        }
    }
    if($restored | Where-Object {-not $_.restored}){throw "Preference restoration failed: $output"}
}

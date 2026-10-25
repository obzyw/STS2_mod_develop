param(
    [Parameter(Mandatory=$true)][string]$Card,
    [string]$Cases,
    [string]$GamePath,
    [int]$TimeoutSeconds = 600,
    [switch]$Probe,
    [switch]$Control,
    [switch]$SilentProbe,
    [switch]$EffectsProbe,
    [switch]$EffectsCompat,
    [switch]$UseInstalledEffects,
    [switch]$TrialEffects,
    [switch]$NativeRenderer,
    [switch]$RegentProbe,
    [switch]$WatcherPoseProbe,
    [switch]$StanceTransitionProbe,
    [switch]$Unified
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $GamePath) { $GamePath = Split-Path $projectRoot -Parent }
if (Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue) { throw 'Close existing game instances before diagnostics.' }
$output = Join-Path $projectRoot ('artifacts/native-' + $Card + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$savedEnvironment = @{}
$variables = @{SteamAppId='2868840'; SteamGameId='2868840'; NECROVFX_TEST='1'; NECROVFX_TEST_CARD=$Card; NECROVFX_TEST_OUTPUT=$output; NECRO_VFX_CAPTURE_PNG='1'; NECROVFX_TEST_CASES=$Cases}
$process = $null
$probeDirectory = Join-Path $GamePath 'mods/STS2CompatProbe'
$probeCreated = $false
$effectsDirectory = Join-Path $GamePath 'mods/SilentEffectsCompat'
$effectsCreated = $false
$effectsInitiallyPresent = Test-Path -LiteralPath $effectsDirectory
$effectsInitialHash = $(if ($effectsInitiallyPresent) { (Get-FileHash -LiteralPath (Join-Path $effectsDirectory 'SilentEffectsCompat.dll')).Hash } else { $null })
$effectsBefore = $null
$necroWasPatched = (Get-FileHash -LiteralPath (Join-Path (Split-Path (Split-Path $GamePath -Parent) -Parent) 'workshop/content/2868840/3821345574/NecrobinderVfx.dll')).Hash -eq '88C0BA2ABB78D8EEA9A4983972449EE3E73A3AB96B8DEE14261DA8F1F8EE1647'
$sideEffects = @()
$userRoot = Join-Path $env:APPDATA 'SlayTheSpire2'
$configSnapshots = @()
$loadEffects = $EffectsProbe -or $EffectsCompat
try {
    if($Unified){$variables['STS2_UNIFIED_TEST']='1'}
    if($StanceTransitionProbe){$variables['STS2_STANCE_TRANSITION_PROBE']='1'}
    if($WatcherPoseProbe){$variables['STS2_WATCHER_POSE_PROBE']='1'}
    if($RegentProbe){
        if(-not $Probe -or $EffectsProbe -or $SilentProbe -or $Control){throw 'RegentProbe requires -Probe alone.'}
        foreach($source in @((Join-Path $env:LOCALAPPDATA 'SlayTheSpire2/RegentFX/ritsu_interop_state.json'),(Join-Path $userRoot 'mod_settings/RegentAnimations.json'))){
            if(-not(Test-Path -LiteralPath $source)){continue}
            $backup=Join-Path $output ('config-before/'+[IO.Path]::GetFileName($source))
            New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $source -Destination $backup
            $configSnapshots+=@{source=$source;backup=$backup;hash=(Get-FileHash -LiteralPath $source).Hash}
        }
        if(Test-Path -LiteralPath (Join-Path $GamePath 'mods/RegentEffectsCompat')){throw 'Remove the temporary prototype before testing author settings only.'}
        $variables['STS2_REGENT_PROBE']='1'
    }
    if ($loadEffects) {
        $steamapps = Split-Path (Split-Path $GamePath -Parent) -Parent
        foreach ($source in @((Join-Path $userRoot 'MeleeAttack_settings.json'),(Join-Path $userRoot 'mod_settings/MoreSilentAnimations.json'),(Join-Path $steamapps 'workshop/content/2868840/3817850774/user_settings.ini'))) {
            if (-not (Test-Path -LiteralPath $source)) { continue }
            $backup = Join-Path $output ('config-before/' + [IO.Path]::GetFileName($source))
            New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $source -Destination $backup
            $configSnapshots += @{source=$source;backup=$backup;hash=(Get-FileHash -LiteralPath $source).Hash}
        }
    }
    if ($Control -and -not $Probe) { throw 'Control mode requires -Probe.' }
    if ($EffectsProbe -and (-not $Probe -or $SilentProbe -or $Control)) { throw 'EffectsProbe requires -Probe alone.' }
    if ($loadEffects) {
        if ($UseInstalledEffects) {
            if (-not (Test-Path -LiteralPath (Join-Path $effectsDirectory 'SilentEffectsCompat.dll'))) { throw 'Installed effects compatibility is missing.' }
        } else {
            if ($TrialEffects -and (Test-Path -LiteralPath $effectsDirectory)) {
                $effectsBefore = Join-Path $output 'effects-before'
                Move-Item -LiteralPath $effectsDirectory -Destination $effectsBefore
            }
            if (Test-Path -LiteralPath $effectsDirectory) { throw 'Effects compatibility is already deployed; refusing to replace it for a temporary test.' }
            & (Join-Path $PSScriptRoot 'Deploy-SilentEffectsCompat.ps1') -GamePath $GamePath
            $effectsCreated = $true
        }
    }
    if ($Probe) {
        if ($Card -ne 'compatibility') { throw 'Probe requires -Card compatibility.' }
        if (Test-Path -LiteralPath $probeDirectory) { throw 'Probe directory already exists; refusing to overwrite.' }
        & dotnet build (Join-Path $projectRoot 'verification/NativeProbe.csproj') -c Release -o (Join-Path $projectRoot 'artifacts/probe-build') "-p:GamePath=$GamePath" --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Probe build failed.' }
        New-Item -ItemType Directory -Path $probeDirectory | Out-Null
        $probeCreated = $true
        Copy-Item -LiteralPath (Join-Path $projectRoot 'artifacts/probe-build/STS2CompatProbe.dll') -Destination $probeDirectory
        Copy-Item -LiteralPath (Join-Path $projectRoot 'verification/NativeProbe.json') -Destination (Join-Path $probeDirectory 'STS2CompatProbe.json')
        $variables['STS2_COMPAT_PROBE'] = '1'
        $variables['STS2_COMPAT_CONTROL'] = $(if ($Control) { '1' } else { '0' })
        $variables['STS2_SILENT_PROBE'] = $(if ($SilentProbe) { '1' } else { '0' })
        $variables['STS2_EFFECTS_PROBE'] = $(if ($EffectsProbe) { '1' } else { '0' })
    }
    foreach ($name in $variables.Keys) {
        $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $variables[$name], 'Process')
    }
    if ($Unified) {
        if($Control -or $TrialEffects -or $EffectsCompat){throw 'Unified mode uses only the installed integrated mod.'}
        if(-not(Test-Path -LiteralPath (Join-Path $GamePath 'mods/STS2UnifiedCompat'))){throw 'Install the unified mod first.'}
    } elseif ($Control) {
        & (Join-Path $PSScriptRoot 'Deploy-NecroCompat.ps1') -GamePath $GamePath -Rollback
    } else { & (Join-Path $PSScriptRoot 'Deploy-NecroCompat.ps1') -GamePath $GamePath }
    # Third-party checkpoint/last-action writers can bypass the game's no-save
    # guards. Snapshot their profile directories before creating a test run.
    $steamData = Join-Path $userRoot 'steam'
    if (Test-Path -LiteralPath $steamData) {
        foreach ($account in Get-ChildItem -LiteralPath $steamData -Directory) {
            $modded = Join-Path $account.FullName 'modded'
            if (-not (Test-Path -LiteralPath $modded)) { continue }
            foreach ($profile in Get-ChildItem -LiteralPath $modded -Directory -Filter 'profile*') {
                foreach ($component in @('rewind','loadout')) {
                    $source = Join-Path $profile.FullName $component
                    $relative = $source.Substring($userRoot.Length + 1)
                    $backup = Join-Path $output ("userdata-before/$relative")
                    $existed = Test-Path -LiteralPath $source
                    if ($existed) {
                        New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
                        Copy-Item -LiteralPath $source -Destination $backup -Recurse
                    }
                    $sideEffects += @{source=$source; relative=$relative; backup=$backup; existed=$existed}
                }
            }
        }
    }
    # Native diagnostics suppress save/replay writes and put their rendered
    # window offscreen. Use real rendering because FramePostDraw is required.
    $log = Join-Path $output 'game.log'
    $renderer = $(if ($NativeRenderer) { '' } else { '--rendering-method gl_compatibility ' })
    $process = Start-Process -FilePath (Join-Path $GamePath 'SlayTheSpire2.exe') -WorkingDirectory $GamePath -ArgumentList ($renderer + "--log-file `"$log`"") -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (-not $process.HasExited -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Seconds 1 }
    if (-not $process.HasExited) { throw "Diagnostic timeout. Evidence: $output" }
    $resultPath = Join-Path $output 'validation.json'
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "No validation report; inspect $log" }
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    Write-Output "Diagnostic report: $resultPath"
    if (-not $result.success -or $process.ExitCode -ne 0) { throw "Diagnostic failed; inspect $resultPath" }
    if ($Probe) {
        $probeFile = $(if ($StanceTransitionProbe) {'stance-transition-validation.json'} elseif ($Unified) {'unified-validation.json'} elseif ($WatcherPoseProbe) {'watcher-pose-validation.json'} elseif ($RegentProbe) {'regent-validation.json'} elseif ($EffectsProbe) {'effects-validation.json'} elseif ($SilentProbe) {'silent-validation.json'} else {'compatibility-probe.json'})
        $probeResult = Get-Content -LiteralPath (Join-Path $output $probeFile) -Raw | ConvertFrom-Json
        $expectedCount = $(if ($Unified -or $StanceTransitionProbe -or $WatcherPoseProbe -or $RegentProbe -or $EffectsProbe) {$probeResult.expectedCases} elseif ($SilentProbe) {12} else {9})
        if (-not $probeResult.success -or @($probeResult.cases).Count -ne $expectedCount -or $expectedCount -lt 1) { throw 'Compatibility probe did not complete all actual-play cases.' }
    }
    Write-Output "PASS: $Card"
} finally {
    # Stop only the exact process created above, then restore the original DLL.
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue; $process.WaitForExit() }
    if ($process) { $process.Dispose() }
    try {
        if ($Unified) { Write-Output 'Unified mode: author gold DLL left untouched.' }
        elseif ($necroWasPatched) { & (Join-Path $PSScriptRoot 'Deploy-NecroCompat.ps1') -GamePath $GamePath }
        else { & (Join-Path $PSScriptRoot 'Deploy-NecroCompat.ps1') -GamePath $GamePath -Rollback }
    } finally {
        if ($effectsCreated -or ($effectsBefore -and (Test-Path -LiteralPath $effectsDirectory))) { & (Join-Path $PSScriptRoot 'Deploy-SilentEffectsCompat.ps1') -GamePath $GamePath -Rollback }
        if ($effectsBefore -and (Test-Path -LiteralPath $effectsBefore)) { Move-Item -LiteralPath $effectsBefore -Destination $effectsDirectory }
        if ($probeCreated) {
            Remove-Item -LiteralPath (Join-Path $probeDirectory 'STS2CompatProbe.dll') -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath (Join-Path $probeDirectory 'STS2CompatProbe.json') -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $probeDirectory
        }
        foreach ($entry in $sideEffects) {
            if (Test-Path -LiteralPath $entry.source) {
                $resolved = (Resolve-Path -LiteralPath $entry.source).Path
                $destination = [IO.Path]::GetFullPath((Join-Path $output ("userdata-after/" + $entry.relative)))
                if (-not $resolved.StartsWith(([IO.Path]::GetFullPath($userRoot) + '\'), [StringComparison]::OrdinalIgnoreCase) -or
                    -not $destination.StartsWith(([IO.Path]::GetFullPath($output) + '\'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe side-effect restore path.' }
                New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
                Move-Item -LiteralPath $resolved -Destination $destination
            }
            if ($entry.existed) { Copy-Item -LiteralPath $entry.backup -Destination $entry.source -Recurse }
        }
        foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
        foreach ($entry in $configSnapshots) {
            if (-not (Test-Path -LiteralPath $entry.source) -or (Get-FileHash -LiteralPath $entry.source).Hash -ne $entry.hash) {
                if (Test-Path -LiteralPath $entry.source) {
                    $after = Join-Path $output ('config-after/' + [IO.Path]::GetFileName($entry.source))
                    New-Item -ItemType Directory -Path (Split-Path $after -Parent) -Force | Out-Null
                    Copy-Item -LiteralPath $entry.source -Destination $after
                }
                Copy-Item -LiteralPath $entry.backup -Destination $entry.source -Force
            }
        }
        $recordChecks = foreach ($entry in $sideEffects) {
            $maps = foreach ($root in @($entry.backup,$entry.source)) {
                $lines = if (Test-Path -LiteralPath $root) {
                    Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object { $_.FullName.Substring($root.Length) + '|' + (Get-FileHash -LiteralPath $_.FullName).Hash } | Sort-Object
                }
                ,(@($lines) -join "`n")
            }
            [pscustomobject]@{path=$entry.relative;matches=($maps[0] -eq $maps[1] -and (Test-Path -LiteralPath $entry.source) -eq $entry.existed)}
        }
        $configChecks = foreach ($entry in $configSnapshots) {
            [pscustomobject]@{path=$entry.source;matches=((Get-FileHash -LiteralPath $entry.source).Hash -eq $entry.hash)}
        }
        $isolation = [pscustomobject]@{
            records=@($recordChecks);configs=@($configChecks)
            probeRemoved=(-not $probeCreated -or -not (Test-Path -LiteralPath $probeDirectory))
            effectsStateRestored=(((Test-Path -LiteralPath $effectsDirectory) -eq $effectsInitiallyPresent) -and
                (-not $effectsInitiallyPresent -or (Get-FileHash -LiteralPath (Join-Path $effectsDirectory 'SilentEffectsCompat.dll')).Hash -eq $effectsInitialHash))
        }
        $isolation | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'isolation.json') -Encoding UTF8
        if (@($recordChecks | Where-Object {-not $_.matches}).Count -or @($configChecks | Where-Object {-not $_.matches}).Count -or -not $isolation.probeRemoved -or -not $isolation.effectsStateRestored) { throw "Isolation restore failed; inspect $output/isolation.json" }
    }
}

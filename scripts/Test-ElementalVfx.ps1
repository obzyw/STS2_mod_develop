param(
    [Parameter(Mandatory=$true)][string]$GamePath,
    [string[]]$Characters = @('Ironclad','Silent','Regent','Necrobinder','Defect'),
    [int]$TimeoutSeconds = 150,
    [switch]$MotionAudit,
    [switch]$UiLayerAudit,
    [switch]$FullSkinAudit,
    [string[]]$SkinCards=@(),
    [string[]]$SkinStages=@('classic','normal','hurt','critical'),
    [ValidateSet('Elemental','Corruption','WatcherTime','WatcherCursor','KnifeTrap','SilentMovement','SilentEffects','SkinAnimation')][string]$Feature='Elemental'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Assert-ChildPath([string]$Parent,[string]$Candidate){
    $parentFull=[IO.Path]::GetFullPath($Parent).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $candidateFull=[IO.Path]::GetFullPath($Candidate)
    if(Test-Path -LiteralPath $candidateFull){$candidateFull=(Resolve-Path -LiteralPath $candidateFull).Path}
    if(-not $candidateFull.StartsWith($parentFull+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Path is outside its expected directory.'}
}
if(Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue){throw 'Close the game normally before running this no-save fixture.'}
if(-not(Get-Process steam -ErrorAction SilentlyContinue)){throw 'Steam must be running to load the subscribed character skins.'}
if($Feature -eq 'Elemental'){
    $parser=Start-Process -FilePath (Join-Path $GamePath 'SlayTheSpire2.exe') -WorkingDirectory $GamePath -ArgumentList @('--headless','--script',('"'+(Join-Path $root 'verification/elemental/CheckScript.gd')+'"'),'--',('"'+(Join-Path $root 'elemental/DefenseFx.gd')+'"')) -Wait -PassThru -WindowStyle Hidden
    if($parser.ExitCode -ne 0){throw 'Native GDScript parse failed.'}
}
$output = Join-Path $root ('artifacts/'+$Feature.ToLower()+'-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $output | Out-Null
$userRoot = Join-Path $env:APPDATA 'SlayTheSpire2'
$backup = Join-Path $output 'userdata-before'
# Preserve saves, third-party checkpoints and preferences; caches and telemetry are not player records.
$excluded = @('logs','shader_cache','vulkan','sentry','analytics')
$before = @{}
foreach($file in Get-ChildItem -LiteralPath $userRoot -Recurse -File){
    $relative = [IO.Path]::GetRelativePath($userRoot,$file.FullName)
    if(($relative -split '[\\/]')[0] -in $excluded){continue}
    $destination = Join-Path $backup $relative
    New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
    $before[$relative]=(Get-FileHash -LiteralPath $file.FullName).Hash
}
$plugin=switch($Feature){'Corruption'{'CorruptionWarriorVfx'} 'WatcherTime'{'WatcherNoBulletTime'} 'WatcherCursor'{'WatcherCookieCursor'} 'KnifeTrap'{'KnifeTrapStages'} 'SilentMovement'{'SilentMovementCompat'} 'SilentEffects'{'SilentEffectsCompat'} 'SkinAnimation'{'SkinAnimationBridge'} default{'ElementalDefenseVfx'}}
$featureFolder=switch($Feature){'Corruption'{'corruption'} 'WatcherTime'{'watcher/no-bullet-time'} 'WatcherCursor'{'watcher/cursor'} 'KnifeTrap'{'effects/knife-trap'} 'SilentMovement'{'compatibility'} 'SilentEffects'{'effects'} 'SkinAnimation'{'skin-animation'} default{'elemental'}}
$testMods = @($plugin,'ElementalProbe')
foreach($name in $testMods){if(Test-Path -LiteralPath (Join-Path $GamePath "mods/$name")){throw "Temporary test refuses to overwrite installed $name"}}
$oldEnv = @{}
$variables=@{SteamAppId='2868840';SteamGameId='2868840';STS2_ELEMENTAL_TEST='1';STS2_ELEMENTAL_OUTPUT=$output;STS2_ELEMENTAL_CHARACTER='';STS2_ELEMENTAL_MOTION=$(if($MotionAudit){'1'}else{'0'});STS2_CORRUPTION_TEST=$(if($Feature -eq 'Corruption'){'1'}else{'0'});STS2_WATCHER_TIME_TEST=$(if($Feature -eq 'WatcherTime'){'1'}else{'0'})}
$process=$null
$variables['STS2_ELEMENTAL_UI_TEST']=$(if($UiLayerAudit){'1'}else{'0'})
$variables['STS2_WATCHER_CURSOR_TEST']=$(if($Feature -eq 'WatcherCursor'){'1'}else{'0'})
$variables['STS2_KNIFE_TRAP_TEST']=$(if($Feature -eq 'KnifeTrap'){'1'}else{'0'})
$variables['STS2_SILENT_MOVEMENT_TEST']=$(if($Feature -eq 'SilentMovement'){'1'}else{'0'})
$variables['STS2_SILENT_EFFECTS_TEST']=$(if($Feature -eq 'SilentEffects'){'1'}else{'0'})
$variables['STS2_SKIN_ANIMATION_TEST']=$(if($Feature -eq 'SkinAnimation'){'1'}else{'0'})
$variables['STS2_SKIN_FULL_AUDIT']=$(if($FullSkinAudit){'1'}else{'0'})
$variables['STS2_SKIN_ONLY_CARDS']=($SkinCards -join ',')
$variables['STS2_SKIN_STAGES']=($SkinStages -join ',')
try {
    & dotnet build (Join-Path $root "$featureFolder/$plugin.csproj") -c Release "-p:GamePath=$GamePath" --nologo
    if($LASTEXITCODE -ne 0){throw 'Effect build failed.'}
    & dotnet build (Join-Path $root 'verification/elemental/ElementalProbe.csproj') -c Release "-p:GamePath=$GamePath" --nologo
    if($LASTEXITCODE -ne 0){throw 'Probe build failed.'}
    foreach($name in $testMods){New-Item -ItemType Directory -Path (Join-Path $GamePath "mods/$name") | Out-Null}
    $runtimeFiles=@("$featureFolder/bin/Release/net9.0/$plugin.dll","$featureFolder/$plugin.json")
    if($Feature -eq 'Elemental'){$runtimeFiles+=@('elemental/DefenseFx.gd','elemental/StoneCoat.gdshader','elemental/FlameTrail.gdshader')}
    if($Feature -eq 'SkinAnimation'){$runtimeFiles+=@('skin-animation/SilentPose.gd','skin-animation/NecroPose.gd','skin-animation/HologramPose.gd')}
    foreach($file in $runtimeFiles){
        Copy-Item -LiteralPath (Join-Path $root $file) -Destination (Join-Path $GamePath "mods/$plugin")
    }
    if(Test-Path -LiteralPath (Join-Path $root "$featureFolder/assets")){Copy-Item -LiteralPath (Join-Path $root "$featureFolder/assets") -Destination (Join-Path $GamePath "mods/$plugin/assets") -Recurse}
    Copy-Item -LiteralPath (Join-Path $root 'verification/elemental/bin/Release/net9.0/ElementalProbe.dll') -Destination (Join-Path $GamePath 'mods/ElementalProbe')
    Copy-Item -LiteralPath (Join-Path $root 'verification/elemental/bin/Release/net9.0/ElementalProbe.pdb') -Destination (Join-Path $GamePath 'mods/ElementalProbe')
    Copy-Item -LiteralPath (Join-Path $root 'verification/elemental/ElementalProbe.json') -Destination (Join-Path $GamePath 'mods/ElementalProbe')
    foreach($name in $variables.Keys){$oldEnv[$name]=[Environment]::GetEnvironmentVariable($name,'Process');[Environment]::SetEnvironmentVariable($name,$variables[$name],'Process')}
    foreach($character in $Characters){
        if(Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue){throw 'Another game process appeared; refusing to start a fixture.'}
        [Environment]::SetEnvironmentVariable('STS2_ELEMENTAL_CHARACTER',$character,'Process')
        $process=Start-Process -FilePath (Join-Path $GamePath 'SlayTheSpire2.exe') -WorkingDirectory $GamePath -ArgumentList @('--rendering-method','gl_compatibility','--log-file',('"'+(Join-Path $output "$character.log")+'"')) -WindowStyle Hidden -PassThru
        if(-not $process.WaitForExit($TimeoutSeconds*1000)){Stop-Process -Id $process.Id;throw "Timed out: $character. See $output"}
        $resultPath=Join-Path $output "$character-result.json"
        if(-not(Test-Path -LiteralPath $resultPath)){throw "Missing result: $character (exit $($process.ExitCode)). See $output"}
        $result=Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        if(-not $result.success){throw "Fixture failed: $character. $($result.error). See $output"}
        if($process.ExitCode -ne 0){throw "Game exit failed: $character ($($process.ExitCode)). See $output"}
        if($Feature -eq 'SkinAnimation' -and $character -ne 'Silent'){Write-Output "$character observation completed. Inspect images and resource paths; this is not visual acceptance."}
        else{Write-Output "$character passed."}
        $process=$null
    }
} finally {
    if($process -and -not $process.HasExited){Stop-Process -Id $process.Id}
    foreach($name in $variables.Keys){if($oldEnv.ContainsKey($name)){[Environment]::SetEnvironmentVariable($name,$oldEnv[$name],'Process')}}
    foreach($name in $testMods){
        $path=Join-Path $GamePath "mods/$name"
        $destination=Join-Path $output $name
        Assert-ChildPath (Join-Path $GamePath 'mods') $path
        Assert-ChildPath (Join-Path $root 'artifacts') $destination
        if(Test-Path -LiteralPath $path){Move-Item -LiteralPath $path -Destination $destination}
    }
    $changes=@()
    foreach($file in Get-ChildItem -LiteralPath $userRoot -Recurse -File){
        $relative=[IO.Path]::GetRelativePath($userRoot,$file.FullName)
        if(($relative -split '[\\/]')[0] -in $excluded){continue}
        $hash=(Get-FileHash -LiteralPath $file.FullName).Hash
        if(-not $before.ContainsKey($relative)){
            $archive=Join-Path $output "userdata-created/$relative"
            Assert-ChildPath $userRoot $file.FullName
            Assert-ChildPath $output $archive
            New-Item -ItemType Directory -Force -Path (Split-Path $archive -Parent) | Out-Null
            Move-Item -LiteralPath $file.FullName -Destination $archive
            $changes+=@{path=$relative;action='archived-test-created'}
        } elseif($hash -ne $before[$relative]){
            Copy-Item -LiteralPath (Join-Path $backup $relative) -Destination $file.FullName -Force
            $changes+=@{path=$relative;action='restored'}
        }
    }
    foreach($relative in $before.Keys){
        $destination=Join-Path $userRoot $relative
        if(-not(Test-Path -LiteralPath $destination)){
            New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
            Copy-Item -LiteralPath (Join-Path $backup $relative) -Destination $destination
        }
        if((Get-FileHash -LiteralPath $destination).Hash -ne $before[$relative]){throw "User record restore failed: $relative"}
    }
    @{restored=$true;originalFileCount=$before.Count;changes=$changes}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $output 'restoration.json')
    Write-Output "Evidence: $output"
}

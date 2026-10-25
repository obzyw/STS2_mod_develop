param([string]$GamePath,[switch]$Rollback)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(-not $GamePath){$GamePath=Split-Path $projectRoot -Parent}
if(Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue){throw 'Close the game first.'}
$target=[IO.Path]::GetFullPath((Join-Path $GamePath 'mods/WatcherDivinityPose'))
if($Rollback){
    if(Test-Path -LiteralPath $target){
        $archive=[IO.Path]::GetFullPath((Join-Path $projectRoot ('artifacts/divinity-rollback-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))))
        if(-not $target.EndsWith('\mods\WatcherDivinityPose') -or -not $archive.StartsWith([IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))+'\')){throw 'Unexpected archive location.'}
        Move-Item -LiteralPath $target -Destination $archive
    }
    Write-Output 'Divinity pose disabled; author files unchanged.'
    exit 0
}
$steamapps=Split-Path (Split-Path $GamePath -Parent) -Parent
$yu=Join-Path $steamapps 'workshop/content/2868840/3817002306/YUWatcherForms.dll'
if((Get-FileHash -LiteralPath $yu).Hash -ne '08BA9AA8606E0AA6FAA0971E537C13D22E523BF6A280D3C169E6942552A6DE37'){throw 'YUWatcherForms was updated; review before deploying.'}
$source=Join-Path $projectRoot 'watcher/pose'
$build=Join-Path $projectRoot 'artifacts/divinity-build'
& dotnet build (Join-Path $source 'WatcherDivinityPose.csproj') -c Release -o $build "-p:GamePath=$GamePath" --nologo
if($LASTEXITCODE -ne 0){throw 'Build failed.'}
if(Test-Path -LiteralPath $target){throw 'Existing pose mod found; roll back before replacing it.'}
New-Item -ItemType Directory -Path $target | Out-Null
Copy-Item -LiteralPath (Join-Path $build 'WatcherDivinityPose.dll'),(Join-Path $source 'WatcherDivinityPose.json'),(Join-Path $source 'DivinityPose.gd') -Destination $target
Copy-Item -LiteralPath (Join-Path $projectRoot 'watcher/assets/divinity/v001/beautified-divinity-body-v002.png') -Destination (Join-Path $target 'arms.png')
Write-Output 'Divinity pose installed as a separate mod.'

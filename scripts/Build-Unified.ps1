param([string]$GamePath,[string]$ModConfigPath)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if(-not $GamePath){$GamePath=Split-Path $projectRoot -Parent}
if(-not(Test-Path -LiteralPath (Join-Path $GamePath 'SlayTheSpire2.exe'))){throw 'Supply -GamePath for this computer.'}
$build=Join-Path $projectRoot 'artifacts/unified-build'
$arguments=@('build',(Join-Path $projectRoot 'unified/STS2UnifiedCompat.csproj'),'-c','Release','-o',$build,"-p:GamePath=$GamePath",'--nologo')
if($ModConfigPath){$arguments+="-p:ModConfigPath=$ModConfigPath"}
& dotnet @arguments
if($LASTEXITCODE -ne 0){throw 'Unified build failed.'}
$package=Join-Path $projectRoot 'artifacts/packages/STS2UnifiedCompat'
New-Item -ItemType Directory -Path $package -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $build 'STS2UnifiedCompat.dll'),(Join-Path $projectRoot 'unified/STS2UnifiedCompat.json'),(Join-Path $projectRoot 'watcher/pose/DivinityPose.gd'),(Join-Path $projectRoot 'watcher/transitions/StanceTransition.gd') -Destination $package -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'watcher/assets/divinity/v001/beautified-divinity-body-v002.png') -Destination (Join-Path $package 'arms.png') -Force
$version=(Get-Content -LiteralPath (Join-Path $package 'STS2UnifiedCompat.json') -Raw -Encoding UTF8 | ConvertFrom-Json).version
Compress-Archive -LiteralPath $package -DestinationPath (Join-Path (Split-Path $package -Parent) "STS2UnifiedCompat-$version.zip") -Force
Write-Output "Built single-mod package: $package"

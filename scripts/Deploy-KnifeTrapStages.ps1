param([Parameter(Mandatory=$true)][string]$GamePath,[switch]$Rollback)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$GamePath=[IO.Path]::GetFullPath($GamePath)
if(-not(Test-Path -LiteralPath (Join-Path $GamePath 'SlayTheSpire2.exe'))){throw 'Supply the game installation directory.'}
if(Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue){throw 'Close the game normally first.'}
$target=[IO.Path]::GetFullPath((Join-Path $GamePath 'mods/KnifeTrapStages'))
if(Test-Path -LiteralPath $target){
    $resolved=(Resolve-Path -LiteralPath $target).Path
    if($resolved -ne $target){throw 'Unexpected install path.'}
    $manifest=Join-Path $target 'KnifeTrapStages.json'
    if(-not(Test-Path -LiteralPath $manifest) -or (Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json).id -ne 'KnifeTrapStages'){throw 'Refusing to replace an unidentified mod directory.'}
}
if(-not $Rollback){
    if((Get-Content -LiteralPath (Join-Path $GamePath 'release_info.json') -Raw|ConvertFrom-Json).version -ne 'v0.111.0'){throw 'This patch targets v0.111.0; recheck before updating.'}
    & dotnet build (Join-Path $root 'effects/knife-trap/KnifeTrapStages.csproj') -c Release "-p:GamePath=$GamePath" --nologo
    if($LASTEXITCODE -ne 0){throw 'Build failed.'}
}
if(Test-Path -LiteralPath $target){
    $archive=[IO.Path]::GetFullPath((Join-Path $root ('artifacts/knife-trap-backup-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))))
    $artifactRoot=[IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
    if(-not $archive.StartsWith($artifactRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe archive path.'}
    New-Item -ItemType Directory -Force -Path $artifactRoot|Out-Null
    Move-Item -LiteralPath $resolved -Destination $archive
    Write-Output "Previous patch preserved: $archive"
}
if($Rollback){Write-Output 'KnifeTrapStages disabled.';exit 0}
New-Item -ItemType Directory -Path $target|Out-Null
Copy-Item -LiteralPath (Join-Path $root 'effects/knife-trap/bin/Release/net9.0/KnifeTrapStages.dll'),(Join-Path $root 'effects/knife-trap/KnifeTrapStages.json') -Destination $target

Write-Output 'Knife Trap stages installed; requires SilentFX and MoreSilentAnimations.'

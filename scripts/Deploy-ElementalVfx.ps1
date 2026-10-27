param([Parameter(Mandatory=$true)][string]$GamePath,[switch]$Rollback)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$GamePath=[IO.Path]::GetFullPath($GamePath)
if(-not(Test-Path -LiteralPath (Join-Path $GamePath 'SlayTheSpire2.exe'))){throw 'Supply the game installation directory.'}
if(Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue){throw 'Close the game normally first.'}
$target=[IO.Path]::GetFullPath((Join-Path $GamePath 'mods/ElementalDefenseVfx'))
if(Test-Path -LiteralPath $target){
    $resolved=(Resolve-Path -LiteralPath $target).Path
    if($resolved -ne $target){throw 'Unexpected install path.'}
    $manifest=Join-Path $target 'ElementalDefenseVfx.json'
    if(-not(Test-Path -LiteralPath $manifest) -or (Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json).id -ne 'ElementalDefenseVfx'){throw 'Refusing to replace an unidentified mod directory.'}
}
if(-not $Rollback){
    if((Get-Content -LiteralPath (Join-Path $GamePath 'release_info.json') -Raw|ConvertFrom-Json).version -ne 'v0.111.0'){throw 'This build was audited against v0.111.0; recheck the API before updating.'}
    & dotnet build (Join-Path $root 'elemental/ElementalDefenseVfx.csproj') -c Release "-p:GamePath=$GamePath" --nologo
    if($LASTEXITCODE -ne 0){throw 'Build failed.'}
}
if(Test-Path -LiteralPath $target){
    $archive=[IO.Path]::GetFullPath((Join-Path $root ('artifacts/elemental-backup-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))))
    $artifactRoot=[IO.Path]::GetFullPath((Join-Path $root 'artifacts'))
    if(-not $archive.StartsWith($artifactRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe archive path.'}
    New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
    Move-Item -LiteralPath $resolved -Destination $archive
    Write-Output "Previous plugin preserved: $archive"
}
if($Rollback){Write-Output 'ElementalDefenseVfx disabled.';exit 0}
New-Item -ItemType Directory -Force -Path $target | Out-Null
foreach($file in @('elemental/bin/Release/net9.0/ElementalDefenseVfx.dll','elemental/ElementalDefenseVfx.json','elemental/DefenseFx.gd','elemental/StoneCoat.gdshader','elemental/FlameTrail.gdshader')){
    Copy-Item -LiteralPath (Join-Path $root $file) -Destination $target
}
Copy-Item -LiteralPath (Join-Path $root 'elemental/assets') -Destination (Join-Path $target 'assets') -Recurse
Write-Output 'ElementalDefenseVfx installed.'

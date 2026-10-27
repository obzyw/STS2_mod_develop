param([string]$GamePath,[switch]$Rollback)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if(-not $GamePath){$GamePath=Split-Path $projectRoot -Parent}
if(-not(Test-Path -LiteralPath (Join-Path $GamePath 'SlayTheSpire2.exe'))){throw 'Supply -GamePath.'}
if(Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue | Where-Object {-not $_.HasExited}){throw 'Close game instances first.'}
$target=[IO.Path]::GetFullPath((Join-Path $GamePath 'mods/SilentMovementCompat'))
if($Rollback){
    if(Test-Path -LiteralPath $target){
        $resolved=(Resolve-Path -LiteralPath $target).Path
        if($resolved -ne $target){throw 'Unexpected install path.'}
        $archive=[IO.Path]::GetFullPath((Join-Path $projectRoot ('artifacts/silent-rollback-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))))
        $artifactRoot=[IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
        if(-not $archive.StartsWith(($artifactRoot+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe archive path.'}
        Move-Item -LiteralPath $resolved -Destination $archive
        Write-Output "Compatibility disabled; preserved in $archive"
    } else {Write-Output 'Already disabled.'}
    exit 0
}
$steamapps=Split-Path (Split-Path $GamePath -Parent) -Parent
$hashes=@{
    '3774274248/MeleeAttack.dll'='7BB0DE0F078D97EDFEBB0D4494B5B0A1224CF75DA940971EC6106870F4AF663B'
    '3821353824/MoreSilentAnimations.dll'='1A86D4BFB5AA9260BF3F22E188DD50D43B9C2ACA39929A20FCC3D2C315956E41'
}
foreach($relative in $hashes.Keys){
    $dll=Join-Path $steamapps ('workshop/content/2868840/'+$relative)
    if((Get-FileHash -LiteralPath $dll).Hash -ne $hashes[$relative]){throw "Re-audit updated mod: $relative"}
}
& dotnet build (Join-Path $projectRoot 'compatibility/SilentMovementCompat.csproj') -c Release -o (Join-Path $projectRoot 'artifacts/silent-build') "-p:GamePath=$GamePath" --nologo
if($LASTEXITCODE -ne 0){throw 'Build failed.'}
$payload=Join-Path $projectRoot 'artifacts/silent-build/SilentMovementCompat.dll'
if(Test-Path -LiteralPath $target){
    if((Get-FileHash -LiteralPath (Join-Path $target 'SilentMovementCompat.dll')).Hash -ne (Get-FileHash -LiteralPath $payload).Hash){throw 'Existing compatibility differs; roll it back before updating.'}
    Write-Output 'Already deployed.'
    exit 0
}
New-Item -ItemType Directory -Path $target | Out-Null
Copy-Item -LiteralPath $payload -Destination $target
Copy-Item -LiteralPath (Join-Path $projectRoot 'compatibility/SilentMovementCompat.json') -Destination $target
Write-Output 'Silent movement compatibility deployed; original workshop DLLs untouched.'

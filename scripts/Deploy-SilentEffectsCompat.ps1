param([string]$GamePath,[switch]$Rollback)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if(-not $GamePath){$GamePath=Split-Path $projectRoot -Parent}
$GamePath=[IO.Path]::GetFullPath($GamePath)
if(-not(Test-Path -LiteralPath (Join-Path $GamePath 'SlayTheSpire2.exe'))){throw 'Supply -GamePath.'}
if(Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue | Where-Object {-not $_.HasExited}){throw 'Close game instances first.'}
$target=Join-Path $GamePath 'mods/SilentEffectsCompat'
if($Rollback){
    if(Test-Path -LiteralPath $target){
        $resolved=(Resolve-Path -LiteralPath $target).Path
        if($resolved -ne [IO.Path]::GetFullPath($target)){throw 'Unexpected install path.'}
        $archive=[IO.Path]::GetFullPath((Join-Path $projectRoot ('artifacts/effects-rollback-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))))
        $artifactRoot=[IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
        if(-not $archive.StartsWith(($artifactRoot+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe archive path.'}
        Move-Item -LiteralPath $resolved -Destination $archive
        Write-Output "Compatibility disabled; preserved in $archive"
    }else{Write-Output 'Already disabled.'}
    exit 0
}
if((Get-Content -LiteralPath (Join-Path $GamePath 'release_info.json') -Raw | ConvertFrom-Json).version -ne 'v0.111.0'){throw 'Re-audit changed game version.'}
if((Get-FileHash -LiteralPath (Join-Path $GamePath 'data_sts2_windows_x86_64/sts2.dll')).Hash -ne '0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9'){throw 'Re-audit changed game binary.'}
$steamapps=Split-Path (Split-Path $GamePath -Parent) -Parent
$hashes=@{
    '3774274248/MeleeAttack.dll'='7BB0DE0F078D97EDFEBB0D4494B5B0A1224CF75DA940971EC6106870F4AF663B'
    '3821353824/MoreSilentAnimations.dll'='95F6E7A7C003FBA2B9FA1C64E470373DCBF349100BB5EB5C0173B7ED5C3D23AC'
    '3817850774/SilentFX.dll'='F0FEF25F3E01F7827F03FA394D366511D09D13826B20689318F474622366CCC7'
}
foreach($relative in $hashes.Keys){
    $dll=Join-Path $steamapps ('workshop/content/2868840/'+$relative)
    if((Get-FileHash -LiteralPath $dll).Hash -ne $hashes[$relative]){throw "Re-audit updated mod: $relative"}
}
& dotnet build (Join-Path $projectRoot 'effects/SilentEffectsCompat.csproj') -c Release -o (Join-Path $projectRoot 'artifacts/effects-build') "-p:GamePath=$GamePath" --nologo
if($LASTEXITCODE -ne 0){throw 'Build failed.'}
$payload=Join-Path $projectRoot 'artifacts/effects-build/SilentEffectsCompat.dll'
if(Test-Path -LiteralPath $target){
    if((Get-FileHash -LiteralPath (Join-Path $target 'SilentEffectsCompat.dll')).Hash -ne (Get-FileHash -LiteralPath $payload).Hash){throw 'Existing compatibility differs; roll it back before updating.'}
    Write-Output 'Already deployed.'
    exit 0
}
New-Item -ItemType Directory -Path $target | Out-Null
Copy-Item -LiteralPath $payload -Destination $target
Copy-Item -LiteralPath (Join-Path $projectRoot 'effects/SilentEffectsCompat.json') -Destination $target
Write-Output 'Silent effects compatibility deployed; original workshop DLLs untouched.'

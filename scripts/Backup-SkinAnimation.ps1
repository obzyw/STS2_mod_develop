param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$GamePath=[IO.Path]::GetFullPath($GamePath)
if(Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue){throw 'Close the game normally before a consistent rollback snapshot.'}
$workshop=Join-Path (Split-Path (Split-Path $GamePath -Parent) -Parent) 'workshop/content/2868840'
$destination=Join-Path $root ('artifacts/skin-animation-backup-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $destination|Out-Null
$ids=@('3815865149','3815873270','3819703745','3819703784','3798368044','3810696310','3816316338','3821353824')
$files=@()
foreach($id in $ids){
    $source=Join-Path $workshop $id
    if(-not(Test-Path -LiteralPath $source)){throw "Required workshop item missing: $id"}
    Copy-Item -LiteralPath $source -Destination (Join-Path $destination $id) -Recurse
    foreach($file in Get-ChildItem -LiteralPath $source -File -Recurse){
        $files+=@{workshopId=$id;relativePath=[IO.Path]::GetRelativePath($source,$file.FullName);sha256=(Get-FileHash -LiteralPath $file.FullName).Hash}
    }
}
Copy-Item -LiteralPath (Join-Path $GamePath 'mods') -Destination (Join-Path $destination 'local-mods') -Recurse
$userRoot=Join-Path $env:APPDATA 'SlayTheSpire2'
foreach($name in @('mod_settings','mod_configs','MeleeAttack_settings.json')){
    $path=Join-Path $userRoot $name
    if(Test-Path -LiteralPath $path){Copy-Item -LiteralPath $path -Destination (Join-Path $destination $name) -Recurse}
}
$files|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $destination 'author-file-hashes.json')
Write-Output "Local rollback snapshot: $destination"
Write-Output 'Contains author resources; keep under ignored artifacts, do not publish.'

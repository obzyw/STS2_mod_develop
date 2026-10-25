param([string]$GamePath)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if(-not $GamePath){$GamePath=Split-Path $projectRoot -Parent}
$workshop=Join-Path (Split-Path (Split-Path $GamePath -Parent) -Parent) 'workshop/content/2868840'
$output=Join-Path $projectRoot ('artifacts/watcher-divinity-baseline-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$rows=@()
foreach($id in @('3747526116','3747800917','3817002306','3818575110')){
    $source=Join-Path $workshop $id
    if(-not(Test-Path -LiteralPath $source)){throw "Missing workshop item: $id"}
    $target=Join-Path $output ('sources/'+$id)
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse
    foreach($file in Get-ChildItem -LiteralPath $source -Recurse -File){
        $relative=$file.FullName.Substring($source.Length+1)
        $hash=(Get-FileHash -LiteralPath $file.FullName).Hash
        if((Get-FileHash -LiteralPath (Join-Path $target $relative)).Hash -ne $hash){throw "Snapshot mismatch: $id/$relative"}
        $rows+=@{workshopId=$id;relativePath=$relative;length=$file.Length;sha256=$hash}
    }
}
$rows | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'sources.json') -Encoding UTF8
Write-Output $output

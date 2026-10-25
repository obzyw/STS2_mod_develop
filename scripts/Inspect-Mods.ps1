param([string]$GamePath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent

# Prefer the enclosing game folder; otherwise discover Steam libraries.
if (-not $GamePath) {
    $candidate = Split-Path $projectRoot -Parent
    if (Test-Path -LiteralPath (Join-Path $candidate 'SlayTheSpire2.exe')) { $GamePath = $candidate }
}
if (-not $GamePath) {
    $steamPath = (Get-ItemProperty 'HKCU:\Software\Valve\Steam').SteamPath
    $libraries = @($steamPath)
    $vdf = Get-Content -LiteralPath (Join-Path $steamPath 'steamapps/libraryfolders.vdf') -Raw
    $libraries += [regex]::Matches($vdf, '"path"\s+"([^"]+)"') | ForEach-Object { $_.Groups[1].Value.Replace('\\', '\') }
    foreach ($library in $libraries) {
        $candidate = Join-Path $library 'steamapps/common/Slay the Spire 2'
        if (Test-Path -LiteralPath (Join-Path $candidate 'SlayTheSpire2.exe')) { $GamePath = $candidate; break }
    }
}
if (-not $GamePath -or -not (Test-Path -LiteralPath (Join-Path $GamePath 'SlayTheSpire2.exe'))) {
    throw 'Game not found. Supply -GamePath.'
}
$GamePath = (Resolve-Path -LiteralPath $GamePath).Path
$steamapps = Split-Path (Split-Path $GamePath -Parent) -Parent
$workshop = Join-Path $steamapps 'workshop/content/2868840'
$userData = Join-Path $env:APPDATA 'SlayTheSpire2'
$output = Join-Path $projectRoot ('artifacts/baseline-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$mods = foreach ($root in @($workshop, (Join-Path $GamePath 'mods'))) {
    if (-not (Test-Path -LiteralPath $root)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.json') {
        try { $manifest = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json } catch { continue }
        if (-not $manifest.id -or -not $manifest.name) { continue }
        $dlls = @(Get-ChildItem -LiteralPath $file.DirectoryName -File -Filter '*.dll' | ForEach-Object {
            @{ name = $_.Name; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
        })
        [pscustomobject]@{ id=$manifest.id; name=$manifest.name; version=$manifest.version; manifest=$file.FullName; dlls=$dlls }
    }
}
@{ game=$GamePath; workshop=$workshop; userData=$userData; release=(Get-Content -LiteralPath (Join-Path $GamePath 'release_info.json') -Raw | ConvertFrom-Json); mods=@($mods) } |
    ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $output 'inventory.json') -Encoding UTF8
foreach ($relative in @('mod_configs/DebilitateRedKnife.animations.json','mod_settings/MoreNecrobinderAnimations.json','logs/godot.log')) {
    $source = Join-Path $userData $relative
    if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $output }
}
Write-Output "Inventoried $(@($mods).Count) manifests. Baseline: $output"

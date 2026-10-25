param([string]$GamePath, [switch]$Rollback)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $GamePath) { $GamePath = Split-Path $projectRoot -Parent }
if (-not (Test-Path -LiteralPath (Join-Path $GamePath 'SlayTheSpire2.exe'))) { throw 'Supply -GamePath.' }
if (Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited }) { throw 'Close the game before deploying or rolling back.' }
$steamapps = Split-Path (Split-Path $GamePath -Parent) -Parent
$workshop = Join-Path $steamapps 'workshop/content/2868840'
$target = Join-Path $workshop '3821345574/NecrobinderVfx.dll'
$build = Join-Path $projectRoot 'artifacts/build/necro-v1'
$report = Get-Content -LiteralPath (Join-Path $build 'build-report.json') -Raw | ConvertFrom-Json
$backupDir = Join-Path $projectRoot ('artifacts/backups/' + $report.originalSha256)
$backup = Join-Path $backupDir 'NecrobinderVfx.dll'
$current = (Get-FileHash -LiteralPath $target).Hash
if ($current -notin @($report.originalSha256,$report.outputSha256)) { throw 'Workshop DLL changed. Refusing to overwrite an unknown version.' }
if ($Rollback) {
    if ($current -eq $report.originalSha256) { Write-Output 'Already original.'; exit 0 }
    if ((Get-FileHash -LiteralPath $backup).Hash -ne $report.originalSha256) { throw 'Backup integrity check failed.' }
    Copy-Item -LiteralPath $backup -Destination $target
    Write-Output 'Original DLL restored.'
    exit 0
}
# This fixed compatibility profile requires both companions; no user settings are changed.
$release = Get-Content -LiteralPath (Join-Path $GamePath 'release_info.json') -Raw | ConvertFrom-Json
if ($release.version -ne 'v0.111.0') { throw 'Unsupported game version; re-audit required.' }
$companions = @{
    '3816255707/DebilitateRedKnife.dll' = '7AB9BD818BBC19DCA5BF7DDA21D82B6666F8C477DA75086C39C6A296AC6C7DE4'
    '3816316338/MoreNecrobinderAnimations.dll' = '6FD8DD9BEB489834EAFFAD4E0CA8D8CF7403C7D4F490EF2A4E814E4F51694005'
    '3816316338/MoreNecrobinderAnimations.BaseLib.dll' = '0AEC870C6D3BAC413C94AA692A177718B7A29B21D572590A4A16C469A819C606'
    '3816316338/MoreNecrobinderAnimations.Settings.dll' = '656FF407B58FD1EC108FF199E38A786EE5127B09A74EF135BA200E88770A566D'
}
foreach ($relative in $companions.Keys) {
    $companion = Join-Path $workshop $relative
    if (-not (Test-Path -LiteralPath $companion)) { throw "Missing companion: $relative" }
    if ((Get-FileHash -LiteralPath $companion).Hash -ne $companions[$relative]) { throw "Companion changed; re-audit required: $relative" }
}
$payload = Join-Path $build 'NecrobinderVfx.dll'
if ($report.staticCheck -ne 'PASS' -or (Get-FileHash -LiteralPath $payload).Hash -ne $report.outputSha256) { throw 'Build integrity check failed.' }
if ($current -eq $report.outputSha256) { Write-Output 'Already deployed.'; exit 0 }
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
if (Test-Path -LiteralPath $backup) {
    if ((Get-FileHash -LiteralPath $backup).Hash -ne $report.originalSha256) { throw 'Existing backup is invalid.' }
} else { Copy-Item -LiteralPath $target -Destination $backup }
Copy-Item -LiteralPath $payload -Destination $target
if ((Get-FileHash -LiteralPath $target).Hash -ne $report.outputSha256) { throw 'Deployment hash check failed.' }
Write-Output "Deployed necro-v1. Backup: $backup"

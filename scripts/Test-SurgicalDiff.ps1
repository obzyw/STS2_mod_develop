param([string]$CecilPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $CecilPath) { $CecilPath = Join-Path $projectRoot 'tools/Mono.Cecil.dll' }
Add-Type -LiteralPath $CecilPath
$build = Join-Path $projectRoot 'artifacts/build/necro-v1'
$report = Get-Content -LiteralPath (Join-Path $build 'build-report.json') -Raw | ConvertFrom-Json
$original = Join-Path $projectRoot ("artifacts/backups/$($report.originalSha256)/NecrobinderVfx.dll")
$before = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($original)
$after = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $build 'NecrobinderVfx.dll'))
function Get-Methods($types) {
    foreach ($type in $types) {
        foreach ($method in $type.Methods) { $method }
        Get-Methods $type.NestedTypes
    }
}
$methods = @{}
foreach ($method in (Get-Methods $before.MainModule.Types)) { $methods[$method.FullName] = $method }
$changes = @()
$count = 0
foreach ($method in (Get-Methods $after.MainModule.Types)) {
    $count++
    if (-not $methods.ContainsKey($method.FullName)) { throw "New method: $($method.FullName)" }
    $old = $methods[$method.FullName]
    $oldBody = ($old.Body.Instructions | ForEach-Object ToString) -join "`n"
    $newBody = ($method.Body.Instructions | ForEach-Object ToString) -join "`n"
    if ($oldBody -cne $newBody) { $changes += $method.FullName }
}
$allowed = @('System.Void NecrobinderVfx.Main::Initialize()', 'System.Boolean NecrobinderVfx.Main::Supported(MegaCrit.Sts2.Core.Models.CardModel)')
if ($count -ne $methods.Count -or $changes.Count -ne 2 -or (Compare-Object $changes $allowed)) { throw "Unexpected method changes: $($changes -join ', ')" }
$before.Dispose()
$after.Dispose()
@{ success=$true; methodsCompared=$count; changedMethods=$changes } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $build 'surgical-diff.json') -Encoding UTF8
Write-Output "PASS: $count method bodies compared; only Initialize and Supported changed."

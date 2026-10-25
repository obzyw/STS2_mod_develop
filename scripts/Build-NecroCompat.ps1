param(
    [Parameter(Mandatory=$true)][string]$InputDll,
    [string]$CecilPath,
    [string]$GamePath
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $CecilPath) { $CecilPath = Join-Path $projectRoot 'tools/Mono.Cecil.dll' }
if (-not $GamePath) { $GamePath = Split-Path $projectRoot -Parent }
if (-not (Test-Path -LiteralPath (Join-Path $GamePath 'data_sts2_windows_x86_64/sts2.dll'))) { throw 'Supply -GamePath for assembly resolution.' }
$expectedHash = '1C68A4699FDC0BB25BC0737FE811E9A368442A47D922F9A6B3F493DED28433AA'
if ((Get-FileHash -LiteralPath $InputDll).Hash -ne $expectedHash) { throw 'Unsupported original DLL. Re-audit updated mod before patching.' }
Add-Type -LiteralPath (Resolve-Path -LiteralPath $CecilPath).Path
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory((Join-Path $GamePath 'data_sts2_windows_x86_64'))
$parameters = [Mono.Cecil.ReaderParameters]::new()
$parameters.AssemblyResolver = $resolver
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $InputDll).Path, $parameters)
$main = $assembly.MainModule.GetType('NecrobinderVfx.Main')
$initialize = @($main.Methods | Where-Object Name -EQ 'Initialize')[0]
$supported = @($main.Methods | Where-Object Name -EQ 'Supported')[0]
$excluded = @('BansheesCry','Eradicate','EndOfDays','Neurosurge','ReaperForm')
$disabledInstallers = @('EndOfDaysPresentation','NeurosurgeRelay','ReaperFormRelay')
$changed = @()

# Leave the companion mods' shared-card hooks untouched. Pop consumes the
# Harmony argument already on the stack without installing the conflicting hook.
foreach ($name in $disabledInstallers) {
    $calls = @($initialize.Body.Instructions | Where-Object {
        $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -and $_.Operand.DeclaringType.FullName -eq "NecrobinderVfx.$name" -and $_.Operand.Name -eq 'Install'
    })
    if ($calls.Count -ne 1) { throw "Expected exactly one installer: $name" }
    $calls[0].OpCode = [Mono.Cecil.Cil.OpCodes]::Pop
    $calls[0].Operand = $null
    $changed += "$name.Install suppressed"
}

# Rebuild only the existing type predicate. Main uses it for drag, play, hit,
# power and deferred callbacks, so one gate suppresses all five shared cards.
$cardTypes = @($supported.Body.Instructions | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Isinst } | ForEach-Object Operand)
if ($cardTypes.Count -ne 26) { throw 'Unexpected Supported type list.' }
foreach ($name in $excluded) { if (@($cardTypes | Where-Object Name -EQ $name).Count -ne 1) { throw "Missing shared card $name" } }
$supported.Body = [Mono.Cecil.Cil.MethodBody]::new($supported)
$il = $supported.Body.GetILProcessor()
$yes = $il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_1)
foreach ($type in $cardTypes | Where-Object { $_.Name -notin $excluded }) {
    $il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0))
    $il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Isinst, $type))
    $il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Brtrue, $yes))
}
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Ldc_I4_0))
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Ret))
$il.Append($yes)
$il.Append($il.Create([Mono.Cecil.Cil.OpCodes]::Ret))
$changed += 'Supported: 26 -> 21 card types'

# Identify this build accurately; the original DLL has an obsolete load banner.
$banner = @($initialize.Body.Instructions | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldstr -and $_.Operand -like '[[]NecrobinderVfx] *loaded:*' })
if ($banner.Count -ne 1) { throw 'Load banner not found.' }
$banner[0].Operand = '[NecrobinderVfx] STS2ModCompat necro-v1 loaded; 21 exclusive card types; shared cards delegated to companion mods.'
$output = Join-Path $projectRoot 'artifacts/build/necro-v1'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$target = Join-Path $output 'NecrobinderVfx.dll'
$assembly.Write($target)
$assembly.Dispose()

# Re-open output and verify both exclusions and preserved non-overlap coverage.
$check = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($target)
$checkMain = $check.MainModule.GetType('NecrobinderVfx.Main')
$checkSupported = @($checkMain.Methods | Where-Object Name -EQ 'Supported')[0]
$actualTypes = @($checkSupported.Body.Instructions | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Isinst } | ForEach-Object { $_.Operand.Name })
$wantedTypes = @($cardTypes | Where-Object { $_.Name -notin $excluded } | ForEach-Object Name)
if ($actualTypes.Count -ne 21 -or (Compare-Object $actualTypes $wantedTypes)) { throw 'Coverage regression.' }
$checkInit = @($checkMain.Methods | Where-Object Name -EQ 'Initialize')[0]
foreach ($name in $disabledInstallers) {
    if (@($checkInit.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq "NecrobinderVfx.$name" -and $_.Operand.Name -eq 'Install' }).Count) { throw "Installer still active: $name" }
}
$check.Dispose()
@{ profile='necro-v1'; originalSha256=$expectedHash; outputSha256=(Get-FileHash -LiteralPath $target).Hash; excludedCards=$excluded; retainedCards=$actualTypes; changes=$changed; staticCheck='PASS'; runtimeCheck='PENDING' } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'build-report.json') -Encoding UTF8
Write-Output "Static checks PASS. Built $target"

param(
    [Parameter(Mandatory=$true)][string]$DllPath,
    [string]$TypeName,
    [string]$GamePath
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $GamePath) { $GamePath = Split-Path $projectRoot -Parent }
$tool = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'tools/.store/ilspycmd') -Recurse -File -Filter 'ilspycmd.dll' | Select-Object -First 1
if (-not $tool) { throw 'Install project-local ilspycmd as described in docs/IMPLEMENTATION.md.' }
$name = [IO.Path]::GetFileNameWithoutExtension($DllPath)
$output = Join-Path $projectRoot "artifacts/decompiled/$name"
New-Item -ItemType Directory -Path $output -Force | Out-Null
$arguments = @('-r', (Join-Path $GamePath 'data_sts2_windows_x86_64'), '-o', $output)
if ($TypeName) { $arguments += @('-t', $TypeName) } else { $arguments += '-p' }
# Invoke the managed entry point: tool shims can embed the old machine's path.
& dotnet $tool.FullName @arguments $DllPath
if ($LASTEXITCODE -ne 0) { throw 'Decompilation failed.' }
Write-Output "Code written to $output"

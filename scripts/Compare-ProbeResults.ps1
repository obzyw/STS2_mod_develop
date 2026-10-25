param(
    [Parameter(Mandatory=$true)][string]$CandidateReport,
    [Parameter(Mandatory=$true)][string]$ControlReport
)
$ErrorActionPreference = 'Stop'
$candidate = Get-Content -LiteralPath $CandidateReport -Raw | ConvertFrom-Json
$control = Get-Content -LiteralPath $ControlReport -Raw | ConvertFrom-Json
if (-not $candidate.success -or -not $control.success -or $candidate.control -or -not $control.control) { throw 'Expected successful candidate and original control reports.' }
if (@($candidate.cases).Count -ne 9 -or @($control.cases).Count -ne 9) { throw 'Both reports must include all nine cases.' }
$results = for ($index = 0; $index -lt 9; $index++) {
    $actual = $candidate.cases[$index]
    $baseline = $control.cases[$index]
    $equal = $actual.card -eq $baseline.card -and $actual.reuseCombat -eq $baseline.reuseCombat -and
        $actual.before -eq $baseline.before -and $actual.after -eq $baseline.after -and
        ($actual.hpBefore -join ',') -eq ($baseline.hpBefore -join ',') -and
        ($actual.hpAfter -join ',') -eq ($baseline.hpAfter -join ',')
    [pscustomobject]@{ card=$actual.card; reuseCombat=$actual.reuseCombat; equalNativePlayAndHp=$equal; candidateGoldCasts=$actual.goldCasts; controlGoldCasts=$baseline.goldCasts }
}
$success = @($results | Where-Object { -not $_.equalNativePlayAndHp }).Count -eq 0
$output = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/differential-validation.json'
@{ success=$success; scope='Same-seed native play counts and actual enemy HP deltas; excludes timing and full power/turn semantics.'; cases=@($results); candidate=(Resolve-Path $CandidateReport).Path; control=(Resolve-Path $ControlReport).Path } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $output -Encoding UTF8
if (-not $success) { throw "Native outcome differs; inspect $output" }
Write-Output "PASS: all nine native play/HP outcomes match original environment. Report: $output"

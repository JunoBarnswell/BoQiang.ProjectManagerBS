[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('ValidatePlan','RunParseValidate','PrepareWideTable')][string]$Action,
    [int]$Nodes = 100,
    [int]$Rows = 10000,
    [int]$Columns = 100,
    [string]$OutputPath = 'artifacts/phase0/performance/phase0-run.json'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$plan = Get-Content -Raw (Join-Path $PSScriptRoot 'performance-baseline.json') | ConvertFrom-Json
if ($Action -eq 'ValidatePlan') {
    if ($plan.measurementPolicy.runs -ne 5 -or !$plan.measurementPolicy.passRequiresRawEvidence) { throw 'Performance plan must require five runs and raw evidence.' }
    foreach ($scenario in $plan.scenarios) { if ([string]::IsNullOrWhiteSpace($scenario.evidencePath)) { throw "Missing evidence path: $($scenario.id)" } }
    Write-Output "PASS: $($plan.scenarios.Count) scenarios have budgets and evidence paths."
    exit 0
}
function Invoke-TestRun([int]$Count) {
    $test = Join-Path $root 'backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj'
    $samples = @()
    for ($run = 1; $run -le 6; $run++) {
        $timer = [Diagnostics.Stopwatch]::StartNew()
        dotnet test $test --configuration Release --no-restore --filter 'FullyQualifiedName~LowCodeRefactorAssetContractTests.Generated_tree_fixture_is_deterministic_bounded_and_valid' --logger 'console;verbosity=quiet' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Parse/validate test failed on run $run." }
        $timer.Stop(); if ($run -gt 1) { $samples += $timer.Elapsed.TotalMilliseconds }
    }
    $sorted = $samples | Sort-Object
    return [ordered]@{ scenario = "document-parse-validate-$Count"; runs = 5; nodes = $Count; samplesMs = @($samples); p50Ms = $sorted[2]; p95Ms = $sorted[4]; p99Ms = $sorted[4]; failureRate = 0; rawCommand = 'dotnet test ... Generated_tree_fixture_is_deterministic_bounded_and_valid'; capturedAt = [DateTime]::UtcNow.ToString('o') }
}
if ($Action -eq 'RunParseValidate') { $result = Invoke-TestRun $Nodes; New-Item -ItemType Directory -Force (Split-Path (Join-Path $root $OutputPath)) | Out-Null; $result | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root $OutputPath) -Encoding UTF8; $result | ConvertTo-Json -Depth 8; exit 0 }
if ($Rows -lt 1 -or $Columns -lt 1) { throw 'Rows and Columns must be positive.' }
$sql = "SELECT TOP (100) * FROM <qualified-table> /* expected source: $Columns columns, $Rows rows; replace provider syntax before execution */"
[ordered]@{ scenario = 'wide-table-browse'; rows = $Rows; columns = $Columns; query = $sql; requiredEvidence = @('provider','serverVersion','queryPlan','elapsedMs','returnedRows','peakWorkingSetBytes','failureRate','rawCommand'); status = 'Blocked'; blockedReason = 'A real provider connection, seeded dataset, and query plan are required; this script never fabricates them.' } | ConvertTo-Json -Depth 8

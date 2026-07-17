param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$project = Join-Path $repositoryRoot "backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj"

Write-Host "Running latest-only deletion guard from $repositoryRoot"
dotnet test $project --configuration $Configuration --no-restore --filter "FullyQualifiedName~LatestOnlySourceScanGuardTests"
if ($LASTEXITCODE -ne 0) {
    Write-Error "FAIL: latest-only source guard detected a forbidden reference or a blocked scan."
    exit $LASTEXITCODE
}

Write-Host "PASS: latest-only source guard"

param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [string]$OutputPath = (Join-Path $RepositoryRoot 'artifacts/hao-105-110/acceptance-matrix.json')
)

$ErrorActionPreference = 'Stop'
$matrixPath = Join-Path $RepositoryRoot 'docs/low-code-refactor/hao-105-110-acceptance-matrix.json'
$workflowPath = Join-Path $RepositoryRoot '.github/workflows/low-code-quality-gates.yml'
$externalEvidenceContractPath = Join-Path $RepositoryRoot 'docs/low-code-refactor/external-evidence-contract.json'
$matrix = Get-Content -Raw -LiteralPath $matrixPath | ConvertFrom-Json
$workflow = Get-Content -Raw -LiteralPath $workflowPath
$externalEvidenceContract = Get-Content -Raw -LiteralPath $externalEvidenceContractPath | ConvertFrom-Json
$expected = 105..110 | ForEach-Object { "HAO-$($_)" }
$actual = @($matrix.issues | ForEach-Object { $_.id })
$missing = @($expected | Where-Object { $_ -notin $actual })
$duplicate = @($actual | Group-Object | Where-Object Count -gt 1 | ForEach-Object Name)
$invalid = @($matrix.issues | Where-Object { $_.localChecks.Count -eq 0 -or [string]::IsNullOrWhiteSpace($_.localStatus) -or [string]::IsNullOrWhiteSpace($_.externalStatus) })
$missingFiles = @($matrix.issues.localChecks | ForEach-Object { if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot $_))) { $_ } })
$workflowRequiredTokens = @(
    'scan-latest-only.ps1',
    'LatestOnlySourceScanGuardTests',
    'LatestOnlyDeletionAcceptanceTests',
    'ApplicationDevelopmentMigrationTests',
    'RuntimePageSchemaServiceTests',
    'RuntimeArtifactIntegrity.test.ts',
    'goldenCases.test.ts',
    'ApplicationDataStudioSqliteIntegrationTests',
    'ApplicationDataSourceExternalProviderGateTests',
    'runtimeSecurityPolicy.test.ts',
    'RuntimeMonitoringContract.test.ts',
    'pageStudioHao111Acceptance.test.ts',
    'pageStudioHao112Acceptance.test.ts',
    'pageStudioHao113Acceptance.test.ts',
    'low-code-external'
)
$missingWorkflowTokens = @($workflowRequiredTokens | Where-Object { $workflow.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -lt 0 })
$executableChecks = @($matrix.issues.localChecks | Where-Object { $_ -match '\.(cs|ts|tsx|ps1)$' })
$missingWorkflowReferences = @(
    $executableChecks |
        ForEach-Object {
            $token = [IO.Path]::GetFileNameWithoutExtension($_)
            if ($workflow.IndexOf($token, [StringComparison]::OrdinalIgnoreCase) -lt 0) { $_ }
        }
)
$invalidStatusTransitions = @($matrix.issues | Where-Object {
    (-not [string]::Equals($_.localStatus, 'Pass', [StringComparison]::OrdinalIgnoreCase) -or
     -not [string]::Equals($_.externalStatus, 'Pass', [StringComparison]::OrdinalIgnoreCase)) -and
    [string]::Equals($_.releaseStatus, 'Pass', [StringComparison]::OrdinalIgnoreCase)
} | ForEach-Object id)
$allowedStatuses = @($matrix.statusVocabulary.PSObject.Properties.Name)
$invalidStatuses = @(
    $matrix.issues | ForEach-Object {
        $issue = $_
        foreach ($field in @('localStatus', 'externalStatus', 'releaseStatus')) {
            $value = [string]$issue.PSObject.Properties[$field].Value
            if ([string]::IsNullOrWhiteSpace($value) -or $value -notin $allowedStatuses) {
                "$($issue.id).$field=$value"
            }
        }
    }
)
$requiredExternalEvidenceFiles = @($externalEvidenceContract.requiredFiles)
$missingExternalEvidenceTokens = @(
    @('external-evidence-contract.json', 'requiredEvidenceFiles', 'requiredFields') |
        Where-Object { $workflow.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -lt 0 }
)
$invalidPolicy = @(
    if (-not $matrix.policy.localPassIsNotReleasePass) { 'policy.localPassIsNotReleasePass must be true' }
    if (-not $matrix.policy.blockedNeverUpgradesToPass) { 'policy.blockedNeverUpgradesToPass must be true' }
    if (-not $matrix.statusVocabulary.Blocked) { 'statusVocabulary.Blocked is required' }
    if (-not (Test-Path -LiteralPath $externalEvidenceContractPath)) { 'external evidence contract is required' }
    if ($externalEvidenceContract.passRules.statusMustEqual -ne 'Pass') { 'external evidence pass status must be Pass' }
    if (-not $externalEvidenceContract.passRules.blockedReasonMustBeEmpty) { 'external evidence blockedReason rule must be true' }
    if ($missingExternalEvidenceTokens.Count -gt 0) { "workflow missing required external evidence contract tokens: $($missingExternalEvidenceTokens -join ', ')" }
)

$result = [ordered]@{
    status = if ($missing.Count -eq 0 -and $duplicate.Count -eq 0 -and $invalid.Count -eq 0 -and $missingFiles.Count -eq 0 -and $missingWorkflowTokens.Count -eq 0 -and $missingWorkflowReferences.Count -eq 0 -and $invalidStatusTransitions.Count -eq 0 -and $invalidStatuses.Count -eq 0 -and $invalidPolicy.Count -eq 0) { 'Pass' } else { 'Fail' }
    matrix = $matrixPath.Substring($RepositoryRoot.Length + 1)
    workflow = $workflowPath.Substring($RepositoryRoot.Length + 1)
    issueCount = $actual.Count
    expectedIssues = $expected
    missingIssues = $missing
    duplicateIssues = $duplicate
    invalidIssues = @($invalid | ForEach-Object id)
    missingCheckFiles = $missingFiles
    missingWorkflowTokens = $missingWorkflowTokens
    missingWorkflowReferences = $missingWorkflowReferences
    invalidStatusTransitions = $invalidStatusTransitions
    invalidStatuses = $invalidStatuses
    missingExternalEvidenceTokens = $missingExternalEvidenceTokens
    invalidPolicy = $invalidPolicy
    externalStatusPolicy = 'Blocked remains Blocked until external evidence is supplied.'
}

$directory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$result | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 -LiteralPath $OutputPath
$result | ConvertTo-Json -Depth 8
if ($result.status -ne 'Pass') { exit 1 }

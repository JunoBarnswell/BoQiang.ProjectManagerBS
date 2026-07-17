[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [ValidateSet('CaptureSnapshot', 'NewEvidence', 'ValidateEvidence', 'HealthCheck', 'RestoreSnapshot', 'CaptureBaseline')]
    [string]$Action,
    [string]$DatabasePath = 'backend/AsterERP.Api/data/application-databases/tenant-a/MES/mes11.db',
    [string]$SnapshotDirectory = 'artifacts/phase0/snapshots',
    [string]$EvidencePath = 'artifacts/phase0/migration-evidence.json',
    [string]$HealthUri = 'http://127.0.0.1:5000/api/health',
    [string]$SnapshotPath,
    [string]$MaintenanceLockId,
    [string]$MigrationId,
    [string]$SourceCommit,
    [string]$TargetCommit,
    [string]$PreviousArtifactId,
    [string]$PublishedArtifactId,
    [string]$HealthCheckId,
    [string]$Operator,
    [string]$TraceId,
    [string]$RollbackReason,
    [string]$RollbackRevisionId,
    [string]$RollbackArtifactId,
    [switch]$AllowRestore
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path

function Resolve-RepoPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path $root $Path))
}

function Get-Sha256([string]$Path) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Missing file: $Path" }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Require-Value([string]$Name, [string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { throw "Missing required value: $Name" }
}

function Write-Json([object]$Value, [string]$Path) {
    $resolved = Resolve-RepoPath $Path
    New-Item -ItemType Directory -Force -Path (Split-Path $resolved) | Out-Null
    $Value | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resolved -Encoding UTF8
    Write-Output $resolved
}

switch ($Action) {
    'CaptureSnapshot' {
        $database = Resolve-RepoPath $DatabasePath
        $directory = Resolve-RepoPath $SnapshotDirectory
        if (!(Test-Path -LiteralPath $database -PathType Leaf)) { throw "Database snapshot source is missing: $database" }
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
        $stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
        $target = Join-Path $directory "$stamp-mes11.db"
        if ($PSCmdlet.ShouldProcess($target, 'Copy read-only database snapshot')) {
            Copy-Item -LiteralPath $database -Destination $target -Force
            Set-ItemProperty -LiteralPath $target -Name IsReadOnly -Value $true
            $record = [ordered]@{ snapshotPath = $target; sourcePath = $database; sha256 = Get-Sha256 $target; bytes = (Get-Item $target).Length; capturedAt = [DateTime]::UtcNow.ToString('o'); readOnly = $true }
            Write-Json $record (Join-Path $SnapshotDirectory "$stamp-mes11.snapshot.json")
        }
    }
    'NewEvidence' {
        foreach ($pair in @(@('migrationId', $MigrationId), @('maintenanceLockId', $MaintenanceLockId), @('sourceCommit', $SourceCommit), @('targetCommit', $TargetCommit), @('previousArtifactId', $PreviousArtifactId), @('publishedArtifactId', $PublishedArtifactId), @('healthCheckId', $HealthCheckId), @('operator', $Operator), @('traceId', $TraceId), @('rollbackReason', $RollbackReason), @('rollbackRevisionId', $RollbackRevisionId), @('rollbackArtifactId', $RollbackArtifactId))) { Require-Value $pair[0] $pair[1] }
        Require-Value 'SnapshotPath' $SnapshotPath
        $backup = Resolve-RepoPath $SnapshotPath
        $record = [ordered]@{ format = 'astererp.low-code.migration-evidence.v1'; status = 'Blocked'; migrationId = $MigrationId; maintenanceLockId = $MaintenanceLockId; backupPath = $backup; backupSha256 = Get-Sha256 $backup; sourceCommit = $SourceCommit; targetCommit = $TargetCommit; previousArtifactId = $PreviousArtifactId; publishedArtifactId = $PublishedArtifactId; healthCheckId = $HealthCheckId; operator = $Operator; traceId = $TraceId; rollbackReason = $RollbackReason; rollbackRevisionId = $RollbackRevisionId; rollbackArtifactId = $RollbackArtifactId; blockedReason = 'Evidence is a template until authenticated health and authorized smoke checks are recorded.'; retryCondition = 'Run HealthCheck after the restarted API and attach the authorized smoke trace.'; createdAt = [DateTime]::UtcNow.ToString('o') }
        Write-Json $record $EvidencePath
    }
    'ValidateEvidence' {
        $path = Resolve-RepoPath $EvidencePath
        $record = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
        $schema = Get-Content -Raw -LiteralPath (Resolve-RepoPath 'docs/low-code-refactor/migration-evidence.schema.json') | ConvertFrom-Json
        foreach ($field in $schema.required) { Require-Value $field ([string]$record.$field) }
        if ($record.status -notin @('Pass','Fail','Blocked')) { throw 'Invalid status.' }
        $actual = Get-Sha256 (Resolve-RepoPath $record.backupPath)
        if ($actual -ne $record.backupSha256.ToLowerInvariant()) { throw 'Backup SHA-256 does not match the evidence record.' }
        if ($record.status -eq 'Blocked') { Require-Value 'blockedReason' $record.blockedReason; Require-Value 'retryCondition' $record.retryCondition }
        if ($record.status -eq 'Fail') { foreach ($field in $schema.rollbackRequired) { Require-Value $field ([string]$record.$field) } }
        Write-Output 'PASS: evidence conforms to required fields and backup hash.'
    }
    'HealthCheck' {
        Require-Value 'HealthUri' $HealthUri
        $response = Invoke-WebRequest -Uri $HealthUri -Method Get -UseBasicParsing -TimeoutSec 15
        if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) { throw "Health check failed with HTTP $($response.StatusCode)." }
        [ordered]@{ healthCheckId = $HealthCheckId; uri = $HealthUri; statusCode = $response.StatusCode; checkedAt = [DateTime]::UtcNow.ToString('o'); bodySha256 = [Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($response.Content)) | ForEach-Object ToString x2 } | ConvertTo-Json
    }
    'RestoreSnapshot' {
        if (!$AllowRestore) { throw 'Restore is destructive. Re-run with -AllowRestore after stopping writes and acquiring the maintenance lock.' }
        Require-Value 'MaintenanceLockId' $MaintenanceLockId
        $source = Resolve-RepoPath $SnapshotPath
        $target = Resolve-RepoPath $DatabasePath
        if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw "Snapshot is missing: $source" }
        if ($PSCmdlet.ShouldProcess($target, "Restore verified snapshot $source")) { Copy-Item -LiteralPath $source -Destination $target -Force; Write-Output "RESTORED: $target" }
    }
    'CaptureBaseline' {
        $tracked = @('global.json','NuGet.Config','backend/AsterERP.Api/AsterERP.Api.csproj','frontend/AsterERP.Web/package-lock.json','docs/low-code-refactor/database-provider-capability-matrix.json')
        $files = foreach ($relative in $tracked) { $path = Resolve-RepoPath $relative; [ordered]@{ path = $relative; sha256 = Get-Sha256 $path; bytes = (Get-Item $path).Length } }
        $commit = (git -C $root rev-parse HEAD).Trim()
        Write-Json ([ordered]@{ format = 'astererp.low-code.phase0-source-baseline.v1'; capturedAt = [DateTime]::UtcNow.ToString('o'); commit = $commit; files = @($files); database = [ordered]@{ path = $DatabasePath; sha256 = Get-Sha256 (Resolve-RepoPath $DatabasePath) }; commands = @('dotnet build AsterERP.sln --configuration Release --no-restore','dotnet test backend/AsterERP.Api.Tests/AsterERP.Api.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~LowCodeBaseline','pwsh -File docs/low-code-refactor/phase0-performance.ps1 -Action ValidatePlan') }) 'artifacts/phase0/source-baseline.json'
    }
}

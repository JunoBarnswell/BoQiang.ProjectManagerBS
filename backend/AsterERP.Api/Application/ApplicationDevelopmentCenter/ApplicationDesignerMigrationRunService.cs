using System.Text.Json;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter;

public sealed class ApplicationDesignerMigrationRunService
{
    public async Task<ApplicationDesignerMigrationRunEntity> AcquireAsync(
        ISqlSugarClient db,
        string tenantId,
        string appCode,
        string migrationKey,
        string backupPayloadJson,
        string? sourceCommit,
        string? targetCommit,
        string? operatorUserId,
        string? traceId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTime.UtcNow;
        var current = (await db.Queryable<ApplicationDesignerMigrationRunEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && item.MigrationKey == migrationKey && !item.IsDeleted)
            .OrderBy(item => item.StartedTime, OrderByType.Desc)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (current?.Status == "ManualRecoveryRequired")
        {
            throw new ValidationException("Designer migration requires manual recovery before retry", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
        if (current?.Status == "Running" && (current.LockExpiresTime is null || current.LockExpiresTime > now))
        {
            throw new ValidationException("Another Designer migration is already running", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
        if (current?.Status == "Running")
        {
            ValidateBackup(current);
            current.Status = "Failed";
            current.DiagnosticsJson = "[{\"code\":\"migration.lock_expired\",\"message\":\"Migration lock expired; immutable backup remains available for recovery.\"}]";
            current.CompletedTime = now;
            current.LockExpiresTime = null;
            await db.Updateable(current).UpdateColumns(item => new { item.Status, item.DiagnosticsJson, item.CompletedTime, item.LockExpiresTime }).Where(item => item.Id == current.Id && item.Status == "Running").ExecuteCommandAsync(cancellationToken);
        }
        if (current is not null)
        {
            ValidateBackup(current);
        }

        var canonicalBackup = ApplicationDesignerCanonicalJson.NormalizeObject(backupPayloadJson);
        var run = new ApplicationDesignerMigrationRunEntity
        {
            TenantId = tenantId,
            AppCode = appCode,
            MigrationKey = migrationKey,
            MaintenanceLockId = Guid.NewGuid().ToString("N"),
            Status = "Running",
            BackupPayloadJson = canonicalBackup,
            BackupSha256 = ApplicationDesignerCanonicalJson.ComputeHash(canonicalBackup),
            SourceCommit = sourceCommit,
            TargetCommit = targetCommit,
            RollbackPointer = current?.RollbackPointer,
            DiagnosticsJson = "[]",
            StartedTime = now,
            LockExpiresTime = now.AddMinutes(30),
            OperatorUserId = operatorUserId,
            TraceId = traceId,
            CreatedBy = operatorUserId,
            CreatedTime = now,
            IsDeleted = false
        };
        try
        {
            await db.Insertable(run).ExecuteCommandAsync(cancellationToken);
            return run;
        }
        catch (Exception exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new ValidationException("Another Designer migration is already running", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    public async Task CompleteAsync(
        ISqlSugarClient db,
        ApplicationDesignerMigrationRunEntity run,
        string healthCheckId,
        string diagnosticsJson = "[]",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(healthCheckId))
        {
            throw new ValidationException("Designer migration health check id is required", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
        run.Status = "Completed";
        run.HealthCheckId = healthCheckId;
        run.DiagnosticsJson = CanonicalDiagnostics(diagnosticsJson);
        run.CompletedTime = DateTime.UtcNow;
        run.LockExpiresTime = null;
        var updated = await db.Updateable(run).UpdateColumns(item => new
        {
            item.Status,
            item.HealthCheckId,
            item.DiagnosticsJson,
            item.CompletedTime,
            item.LockExpiresTime
        }).Where(item => item.Id == run.Id && item.Status == "Running" && !item.IsDeleted).ExecuteCommandAsync(cancellationToken);
        if (updated != 1) throw new ValidationException("Designer migration run is no longer active", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    public async Task FailAsync(
        ISqlSugarClient db,
        ApplicationDesignerMigrationRunEntity run,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        run.Status = "Failed";
        run.DiagnosticsJson = JsonSerializer.Serialize(new[] { new { code = "migration.failed", message = exception.Message } });
        run.CompletedTime = DateTime.UtcNow;
        run.LockExpiresTime = null;
        await db.Updateable(run).UpdateColumns(item => new
        {
            item.Status,
            item.DiagnosticsJson,
            item.CompletedTime,
            item.LockExpiresTime
        }).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ApplicationDesignerMigrationRunEntity> RequireAsync(
        ISqlSugarClient db,
        string tenantId,
        string appCode,
        string runId,
        CancellationToken cancellationToken = default)
    {
        var run = (await db.Queryable<ApplicationDesignerMigrationRunEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && item.Id == runId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        return run ?? throw new ValidationException("Designer migration run was not found", ErrorCodes.ApplicationDataCenterObjectNotFound);
    }

    private static string CanonicalDiagnostics(string diagnosticsJson)
    {
        if (string.IsNullOrWhiteSpace(diagnosticsJson))
        {
            return "[]";
        }

        using var document = JsonDocument.Parse(diagnosticsJson);
        return document.RootElement.GetRawText();
    }

    private static void ValidateBackup(ApplicationDesignerMigrationRunEntity run)
    {
        var canonical = ApplicationDesignerCanonicalJson.NormalizeObject(run.BackupPayloadJson);
        if (!string.Equals(ApplicationDesignerCanonicalJson.ComputeHash(canonical), run.BackupSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Designer migration backup hash is invalid; manual recovery is required", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private static bool IsUniqueConstraintViolation(Exception exception) =>
        exception.ToString().Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
        exception.ToString().Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
        exception.ToString().Contains("duplicate entry", StringComparison.OrdinalIgnoreCase);
}

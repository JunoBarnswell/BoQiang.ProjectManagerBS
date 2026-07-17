using System.Diagnostics;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.Logging;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataCenterSqlScriptAuditWriter(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    ICurrentUser currentUser,
    ILogger<ApplicationDataCenterSqlScriptAuditWriter> logger)
{
    public async Task EnsureAvailableAsync(CancellationToken cancellationToken)
    {
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await db.Queryable<ApplicationSqlScriptAuditEntity>()
            .Take(1)
            .ToListAsync(cancellationToken);
    }

    public async Task<ApplicationDataCenterSqlScriptAuditSummaryResponse> WriteAsync(
        ApplicationSqlScriptAuditEntity audit,
        CancellationToken cancellationToken)
    {
        var workspace = workspaceResolver.Resolve();
        var operatorId = currentUser.GetAsterErpUserId();
        if (string.IsNullOrWhiteSpace(workspace.TenantId) ||
            string.IsNullOrWhiteSpace(workspace.AppCode) ||
            string.IsNullOrWhiteSpace(operatorId))
        {
            throw new ValidationException(
                "Data Studio audit requires a resolved tenant, app and operator.",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        audit.Id = string.IsNullOrWhiteSpace(audit.Id) ? Guid.NewGuid().ToString("N") : audit.Id;
        audit.TenantId = workspace.TenantId;
        audit.AppCode = workspace.AppCode;
        audit.TraceId = string.IsNullOrWhiteSpace(audit.TraceId)
            ? Activity.Current?.Id ?? Guid.NewGuid().ToString("N")
            : audit.TraceId;
        audit.CreatedBy = operatorId;
        audit.CreatedTime = DateTime.UtcNow;
        audit.IsDeleted = false;
        audit.ActorUserId = audit.CreatedBy;
        audit.OccurredAt = audit.CreatedTime;
        audit.Operation = string.IsNullOrWhiteSpace(audit.Operation) ? audit.SourceKind : audit.Operation;
        audit.ResourceKind = string.IsNullOrWhiteSpace(audit.ResourceKind) ? audit.SourceKind : audit.ResourceKind;
        audit.Outcome = string.IsNullOrWhiteSpace(audit.Outcome)
            ? audit.IsSuccess ? "Succeeded" : "Failed"
            : audit.Outcome;
        audit.RedactedDetailsJson = string.IsNullOrWhiteSpace(audit.RedactedDetailsJson) ? "{}" : audit.RedactedDetailsJson;
        audit.RequestHash = string.IsNullOrWhiteSpace(audit.RequestHash) ? audit.ScriptHash : audit.RequestHash;

        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await db.Insertable(audit).ExecuteCommandAsync(cancellationToken);
        logger.LogInformation(
            "SQL script audit written. AuditId={AuditId} TraceId={TraceId} SourceKind={SourceKind} Success={Success}",
            audit.Id,
            audit.TraceId,
            audit.SourceKind,
            audit.IsSuccess);

        return new ApplicationDataCenterSqlScriptAuditSummaryResponse(
            audit.Id,
            audit.TraceId,
            audit.IsSuccess ? "Success" : "Failed",
            audit.StatementSummary,
            audit.DurationMs,
            audit.AffectedRows,
            audit.ReturnedRows,
            audit.ErrorMessage);
    }
}

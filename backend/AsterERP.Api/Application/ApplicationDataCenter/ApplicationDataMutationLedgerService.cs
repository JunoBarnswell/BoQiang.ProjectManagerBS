using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using System.Text.Json;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataMutationLedgerService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    ApplicationDataMutationLedgerSchemaInitializer? schemaInitializer = null)
{
    private static readonly TimeSpan ExecutionLeaseDuration = TimeSpan.FromMinutes(10);
    private const string CrossDatabaseBoundaryReason = "External database and application database are separate transaction boundaries; exactly-once is not claimed.";
    private const string LeaseExpiredFailureCode = "ExecutionLeaseExpired";
    private const string UnknownOutcomeFailureCode = "ExternalWriteUnknown";
    private readonly ApplicationDataMutationLedgerSchemaInitializer ledgerSchemaInitializer = schemaInitializer ?? new();

    public async Task<ApplicationDataMutationLedgerReservationResult> ReserveAsync(
        ApplicationDataMutationLedgerReservation request,
        CancellationToken cancellationToken = default)
    {
        ValidateReservation(request);
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await ledgerSchemaInitializer.EnsureAsync(db, cancellationToken);
        var existing = await FindAsync(db, request.Operation, request.RequestHash, workspace.UserId, cancellationToken);
        if (existing is not null)
        {
            existing = await ExpireLeaseIfNeededAsync(db, existing, cancellationToken);
            return new(existing, false);
        }

        var now = DateTime.UtcNow;
        var ledger = new ApplicationDataMutationLedgerEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ActorUserId = workspace.UserId,
            Operation = request.Operation,
            RequestHash = request.RequestHash,
            Status = ApplicationDataMutationLedgerStatus.Executing,
            ResourceKind = request.ResourceKind,
            ResourceId = request.ResourceId,
            DataSourceId = request.DataSourceId,
            ObjectName = request.ObjectName,
            StatementSummary = request.StatementSummary,
            StatementHash = request.StatementHash,
            Provider = request.Provider,
            ExpectedAffectedRows = request.ExpectedAffectedRows,
            ReservedAt = now,
            ExecutingAt = now,
            AffectedRows = 0,
            LeaseToken = Guid.NewGuid().ToString("N"),
            LeaseExpiresAt = now.Add(ExecutionLeaseDuration),
            StatusHistoryJson = BuildStatusHistoryJson(
                ApplicationDataMutationLedgerStatus.Executing,
                now,
                CrossDatabaseBoundaryReason),
            CreatedBy = workspace.UserId,
            CreatedTime = now,
            IsDeleted = false
        };

        try
        {
            await db.Insertable(ledger).ExecuteCommandAsync(cancellationToken);
            return new(ledger, true);
        }
        catch (Exception exception) when (IsUniqueConstraint(exception))
        {
            var raced = await FindAsync(db, request.Operation, request.RequestHash, workspace.UserId, cancellationToken);
            if (raced is not null)
            {
                raced = await ExpireLeaseIfNeededAsync(db, raced, cancellationToken);
                return new(raced, false);
            }

            throw;
        }
    }

    public async Task<ApplicationDataMutationLedgerEntity> TransitionAsync(
        string ledgerId,
        string targetStatus,
        int affectedRows,
        string? failureCode,
        string? errorMessage,
        string statusReason,
        string? reconcileEvidenceJson = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsKnownStatus(targetStatus) || string.IsNullOrWhiteSpace(statusReason))
        {
            throw new ValidationException("Mutation ledger status transition is invalid.", AsterERP.Shared.ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await ledgerSchemaInitializer.EnsureAsync(db, cancellationToken);
        var ledger = await db.Queryable<ApplicationDataMutationLedgerEntity>()
            .Where(item => item.Id == ledgerId && !item.IsDeleted)
            .FirstAsync(cancellationToken)
            ?? throw new ValidationException("Mutation ledger does not exist.", AsterERP.Shared.ErrorCodes.ApplicationDataCenterObjectNotFound);

        ledger = await ExpireLeaseIfNeededAsync(db, ledger, cancellationToken);
        if (ledger.Status == targetStatus)
        {
            return ledger;
        }

        if (ledger.Status is ApplicationDataMutationLedgerStatus.Finalized or ApplicationDataMutationLedgerStatus.Failed)
        {
            throw new ValidationException("Mutation ledger is already terminal.", AsterERP.Shared.ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        if (ledger.Status is ApplicationDataMutationLedgerStatus.Unknown or ApplicationDataMutationLedgerStatus.RecoveryRequired)
        {
            throw new ValidationException("Mutation ledger requires manual recovery confirmation; automatic transition is forbidden.", AsterERP.Shared.ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        if (ledger.Status == ApplicationDataMutationLedgerStatus.Executing &&
            (ledger.LeaseExpiresAt is null || ledger.LeaseExpiresAt <= DateTime.UtcNow))
        {
            throw new ValidationException("Mutation execution lease has expired; external outcome is unknown and requires manual recovery.", AsterERP.Shared.ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var transitionReason = BuildTransitionReason(targetStatus, failureCode, statusReason);
        var executionLeaseToken = ledger.LeaseToken;
        ledger.Status = targetStatus;
        ledger.AffectedRows = Math.Max(affectedRows, 0);
        ledger.FailureCode = failureCode;
        ledger.ErrorMessage = errorMessage;
        ledger.StatusReason = transitionReason;
        ledger.ReconcileEvidenceJson = reconcileEvidenceJson;
        ledger.LeaseToken = targetStatus == ApplicationDataMutationLedgerStatus.Executing ? ledger.LeaseToken : null;
        ledger.LeaseExpiresAt = targetStatus == ApplicationDataMutationLedgerStatus.Executing ? ledger.LeaseExpiresAt : null;
        ledger.StatusHistoryJson = AppendStatusHistory(ledger.StatusHistoryJson, targetStatus, DateTime.UtcNow, transitionReason);
        ledger.FinalizedAt = targetStatus is ApplicationDataMutationLedgerStatus.Finalized or ApplicationDataMutationLedgerStatus.Failed
            ? DateTime.UtcNow
            : ledger.FinalizedAt;
        ledger.UpdatedTime = DateTime.UtcNow;
        var updated = await db.Updateable(ledger)
            .Where(item => item.Id == ledger.Id &&
                           !item.IsDeleted &&
                           item.Status == ApplicationDataMutationLedgerStatus.Executing &&
                           item.LeaseToken == executionLeaseToken &&
                           item.LeaseExpiresAt > DateTime.UtcNow)
            .ExecuteCommandAsync(cancellationToken);
        if (updated != 1)
        {
            throw new ValidationException("Mutation execution lease was lost before the terminal state was persisted.", AsterERP.Shared.ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return ledger;
    }

    public async Task<ApplicationDataMutationLedgerResponse> GetAsync(
        string ledgerId,
        CancellationToken cancellationToken = default)
    {
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await ledgerSchemaInitializer.EnsureAsync(db, cancellationToken);
        var ledger = await db.Queryable<ApplicationDataMutationLedgerEntity>()
            .Where(item => item.Id == ledgerId && !item.IsDeleted)
            .FirstAsync(cancellationToken)
             ?? throw new ValidationException("Mutation ledger does not exist.", AsterERP.Shared.ErrorCodes.ApplicationDataCenterObjectNotFound);
        ledger = await ExpireLeaseIfNeededAsync(db, ledger, cancellationToken);
        return Map(ledger);
    }

    public async Task<ApplicationDataMutationLedgerReconcileResponse> ReconcileAsync(
        ApplicationDataMutationLedgerReconcileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.BusinessEvidence) || request.BusinessEvidence.Trim().Length < 10)
        {
            throw new ValidationException("Recovery requires business evidence.", AsterERP.Shared.ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        if (request.TargetStatus is not ApplicationDataMutationLedgerStatus.Finalized and not ApplicationDataMutationLedgerStatus.Failed)
        {
            throw new ValidationException("Recovery can only finalize or fail a mutation.", AsterERP.Shared.ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await ledgerSchemaInitializer.EnsureAsync(db, cancellationToken);
        var ledger = await db.Queryable<ApplicationDataMutationLedgerEntity>()
            .Where(item => item.Id == request.LedgerId && !item.IsDeleted)
            .FirstAsync(cancellationToken)
             ?? throw new ValidationException("Mutation ledger does not exist.", AsterERP.Shared.ErrorCodes.ApplicationDataCenterObjectNotFound);
        ledger = await ExpireLeaseIfNeededAsync(db, ledger, cancellationToken);
        if (ledger.Status is ApplicationDataMutationLedgerStatus.Finalized or ApplicationDataMutationLedgerStatus.Failed)
        {
            if (ledger.Status != request.TargetStatus)
            {
                throw new ValidationException("Mutation ledger was already reconciled with a different terminal status.", AsterERP.Shared.ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            return new(Map(ledger), request.TargetStatus, ledger.AffectedRows, request.BusinessEvidence.Trim());
        }

        if (ledger.Status is not ApplicationDataMutationLedgerStatus.Unknown and not ApplicationDataMutationLedgerStatus.RecoveryRequired)
        {
            throw new ValidationException("Mutation ledger does not require recovery.", AsterERP.Shared.ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var workspace = workspaceResolver.Resolve();
        var evidence = global::System.Text.Json.JsonSerializer.Serialize(new
        {
            request.BusinessEvidence,
            request.ExternalAffectedRows,
            source = "manual-recovery",
            occurredAt = DateTime.UtcNow
        });
        ledger.Status = request.TargetStatus;
        ledger.AffectedRows = request.ExternalAffectedRows ?? ledger.AffectedRows;
        ledger.StatusReason = "Manual recovery confirmed with business evidence. " + CrossDatabaseBoundaryReason;
        ledger.ReconcileEvidenceJson = evidence;
        ledger.FailureCode ??= UnknownOutcomeFailureCode;
        ledger.StatusHistoryJson = AppendStatusHistory(
            ledger.StatusHistoryJson,
            request.TargetStatus,
            DateTime.UtcNow,
            "Manual recovery confirmed. " + CrossDatabaseBoundaryReason);
        ledger.ReconciledBy = workspace.UserId;
        ledger.ReconciledAt = DateTime.UtcNow;
        ledger.FinalizedAt = ledger.ReconciledAt;
        ledger.UpdatedBy = workspace.UserId;
        ledger.UpdatedTime = ledger.ReconciledAt;
        await db.Updateable(ledger).WhereColumns(item => item.Id).ExecuteCommandAsync(cancellationToken);
        return new(Map(ledger), request.TargetStatus, ledger.AffectedRows, request.BusinessEvidence.Trim());
    }

    private static async Task<ApplicationDataMutationLedgerEntity?> FindAsync(
        ISqlSugarClient db,
        string operation,
        string requestHash,
        string actorUserId,
        CancellationToken cancellationToken) =>
        await db.Queryable<ApplicationDataMutationLedgerEntity>()
            .Where(item => item.Operation == operation && item.RequestHash == requestHash && item.ActorUserId == actorUserId && !item.IsDeleted)
            .FirstAsync(cancellationToken);

    private static ApplicationDataMutationLedgerResponse Map(ApplicationDataMutationLedgerEntity ledger) => new(
        ledger.Id,
        ledger.Operation,
        ledger.RequestHash,
        ledger.Status,
        ledger.ResourceKind,
        ledger.ResourceId,
        ledger.DataSourceId,
        ledger.ObjectName,
        ledger.ExpectedAffectedRows,
        ledger.AffectedRows,
        ledger.ReservedAt,
        ledger.ExecutingAt,
        ledger.FinalizedAt,
        ledger.FailureCode,
        ledger.ErrorMessage,
        ledger.StatusReason,
        ledger.ReconciledBy,
        ledger.ReconciledAt);

    private static async Task<ApplicationDataMutationLedgerEntity> ExpireLeaseIfNeededAsync(
        ISqlSugarClient db,
        ApplicationDataMutationLedgerEntity ledger,
        CancellationToken cancellationToken)
    {
        if (ledger.Status != ApplicationDataMutationLedgerStatus.Executing ||
            ledger.LeaseExpiresAt is null ||
            ledger.LeaseExpiresAt > DateTime.UtcNow)
        {
            return ledger;
        }

        var now = DateTime.UtcNow;
        ledger.Status = ApplicationDataMutationLedgerStatus.RecoveryRequired;
        ledger.FailureCode = LeaseExpiredFailureCode;
        ledger.ErrorMessage = "Execution lease expired; the external database outcome is unknown and must be verified manually.";
        ledger.StatusReason = "RecoveryRequired: execution lease expired. Automatic retry is forbidden. " + CrossDatabaseBoundaryReason;
        ledger.LeaseToken = null;
        ledger.LeaseExpiresAt = null;
        ledger.StatusHistoryJson = AppendStatusHistory(ledger.StatusHistoryJson, ledger.Status, now, ledger.StatusReason);
        ledger.UpdatedTime = now;
        var updated = await db.Updateable(ledger)
            .Where(item => item.Id == ledger.Id &&
                           !item.IsDeleted &&
                           item.Status == ApplicationDataMutationLedgerStatus.Executing &&
                           item.LeaseExpiresAt <= now)
            .ExecuteCommandAsync(cancellationToken);
        if (updated == 1)
        {
            return ledger;
        }

        return await db.Queryable<ApplicationDataMutationLedgerEntity>()
            .Where(item => item.Id == ledger.Id && !item.IsDeleted)
            .FirstAsync(cancellationToken) ?? ledger;
    }

    private static string BuildStatusHistoryJson(string status, DateTime occurredAt, string reason) =>
        JsonSerializer.Serialize(new[] { new StatusHistoryEntry(status, occurredAt, reason) });

    private static string AppendStatusHistory(string? historyJson, string status, DateTime occurredAt, string reason)
    {
        var history = string.IsNullOrWhiteSpace(historyJson)
            ? []
            : JsonSerializer.Deserialize<List<StatusHistoryEntry>>(historyJson) ?? [];
        history.Add(new(status, occurredAt, reason));
        var bounded = history.TakeLast(64).ToList();
        var serialized = JsonSerializer.Serialize(bounded);
        while (serialized.Length > 15000 && bounded.Count > 1)
        {
            bounded.RemoveAt(0);
            serialized = JsonSerializer.Serialize(bounded);
        }

        return serialized;
    }

    private static string BuildTransitionReason(string targetStatus, string? failureCode, string statusReason)
    {
        var reason = statusReason.Trim();
        if (targetStatus == ApplicationDataMutationLedgerStatus.Failed &&
            !string.Equals(failureCode, UnknownOutcomeFailureCode, StringComparison.OrdinalIgnoreCase))
        {
            reason += " SafeRetryDisposition: the outcome is known; automatic replay is not performed and a new request must be revalidated.";
        }

        return reason.Contains("exactly-once is not claimed", StringComparison.OrdinalIgnoreCase)
            ? reason
            : reason + " " + CrossDatabaseBoundaryReason;
    }

    private sealed record StatusHistoryEntry(string Status, DateTime OccurredAt, string Reason);

    private static void ValidateReservation(ApplicationDataMutationLedgerReservation request)
    {
        if (string.IsNullOrWhiteSpace(request.Operation) || string.IsNullOrWhiteSpace(request.RequestHash) ||
            string.IsNullOrWhiteSpace(request.ResourceKind) || string.IsNullOrWhiteSpace(request.DataSourceId) ||
            string.IsNullOrWhiteSpace(request.ObjectName) || string.IsNullOrWhiteSpace(request.StatementHash))
        {
            throw new ValidationException("Mutation ledger reservation is incomplete.", AsterERP.Shared.ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private static bool IsKnownStatus(string status) => status is
        ApplicationDataMutationLedgerStatus.Executing or
        ApplicationDataMutationLedgerStatus.Finalized or
        ApplicationDataMutationLedgerStatus.Failed or
        ApplicationDataMutationLedgerStatus.Unknown or
        ApplicationDataMutationLedgerStatus.RecoveryRequired;

    private static bool IsUniqueConstraint(Exception exception) =>
        exception.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
}

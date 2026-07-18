using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementOperationWriter(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementOperationProgressPublisher? progressPublisher = null,
    IProjectManagementOperationTransitionObserver? transitionObserver = null) : IProjectManagementOperationWriter
{
    public async Task CreatePendingAsync(string operationId, string operationType, string impactJson, string traceId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entity = new ProjectManagementOperationEntity
        {
            Id = operationId,
            TenantId = Tenant(),
            AppCode = App(),
            OperationType = operationType,
            Status = "Pending",
            Phase = "Queued",
            ProgressPercent = 0,
            ImpactJson = impactJson,
            TraceId = traceId,
            ActorUserId = UserId(),
            StartedTime = now,
            CreatedBy = UserId(),
            CreatedTime = now
        };
        await CreateAndPublishAsync(entity, cancellationToken);
    }

    public async Task StartAsync(string operationId, string operationType, string impactJson, string traceId, CancellationToken cancellationToken = default)
    {
        var entity = await FindOwnedAsync(operationId, cancellationToken);
        if (entity is null)
        {
            var now = DateTime.UtcNow;
            entity = new ProjectManagementOperationEntity
            {
                Id = operationId,
                TenantId = Tenant(),
                AppCode = App(),
                OperationType = operationType,
                Status = "Running",
                Phase = "Starting",
                ImpactJson = impactJson,
                TraceId = traceId,
                ActorUserId = UserId(),
                StartedTime = now,
                CreatedBy = UserId(),
                CreatedTime = now
            };
            await CreateAndPublishAsync(entity, cancellationToken);
            return;
        }
        if (IsTerminal(entity.Status)) return;
        if (entity.IsCancellationRequested)
        {
            await SetTerminalAsync(entity, "Canceled", "Canceled", null, cancellationToken);
            return;
        }

        entity.Status = "Running";
        entity.Phase = "Starting";
        entity.OperationType = operationType;
        entity.ImpactJson = impactJson;
        entity.TraceId = traceId;
        entity.ErrorMessage = null;
        entity.UpdatedBy = UserId();
        entity.UpdatedTime = DateTime.UtcNow;
        await PersistAsync(entity, cancellationToken);
    }

    public async Task<bool> ReportProgressAsync(string operationId, string phase, int progressPercent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phase)) throw new ValidationException("操作阶段不能为空");
        var entity = await GetOwnedAsync(operationId, cancellationToken);
        if (!string.Equals(entity.Status, "Running", StringComparison.Ordinal)) return false;
        if (entity.IsCancellationRequested)
        {
            await SetTerminalAsync(entity, "Canceled", "Canceled", null, cancellationToken);
            return false;
        }

        entity.Phase = phase.Trim().Length > 120 ? phase.Trim()[..120] : phase.Trim();
        entity.ProgressPercent = Math.Clamp(Math.Max(entity.ProgressPercent, progressPercent), 0, 99);
        entity.UpdatedBy = UserId();
        entity.UpdatedTime = DateTime.UtcNow;
        return await PersistAsync(entity, cancellationToken);
    }

    public async Task<bool> IsCancellationRequestedAsync(string operationId, CancellationToken cancellationToken = default) =>
        (await GetOwnedAsync(operationId, cancellationToken)).IsCancellationRequested;

    public async Task RequestCancellationAsync(string operationId, CancellationToken cancellationToken = default)
    {
        var db = databaseAccessor.GetProjectManagementDb();
        var now = DateTime.UtcNow;
        ProjectManagementOperationEntity? entity = null;
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            var affected = await db.Updateable<ProjectManagementOperationEntity>()
                .SetColumns(item => new ProjectManagementOperationEntity
                {
                    IsCancellationRequested = true,
                    CancellationRequestedBy = UserId(),
                    CancellationRequestedTime = now,
                    Phase = "CancellationRequested",
                    UpdatedBy = UserId(),
                    UpdatedTime = now,
                    VersionNo = item.VersionNo + 1
                })
                .Where(item => item.Id == operationId && item.TenantId == Tenant() && item.AppCode == App() && item.ActorUserId == UserId() && !item.IsDeleted && !item.IsCancellationRequested && (item.Status == "Pending" || item.Status == "Running"))
                .ExecuteCommandAsync(cancellationToken);
            if (affected != 1) return;
            entity = await FindOwnedAsync(operationId, cancellationToken);
            if (entity is not null) await InsertEventAsync(db, entity, cancellationToken);
        });
        if (entity is not null) await PublishBestEffortAsync(entity, cancellationToken);
    }

    public async Task CancelAsync(string operationId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(operationId, cancellationToken);
        if (IsTerminal(entity.Status)) return;
        await SetTerminalAsync(entity, "Canceled", "Canceled", null, cancellationToken);
    }

    public async Task SucceedAsync(string operationId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(operationId, cancellationToken);
        if (IsTerminal(entity.Status)) return;
        if (entity.IsCancellationRequested)
        {
            await SetTerminalAsync(entity, "Canceled", "Canceled", null, cancellationToken);
            return;
        }
        await SetTerminalAsync(entity, "Succeeded", "Completed", null, cancellationToken);
    }

    public async Task CompleteWithImpactAsync(string operationId, string impactJson, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(operationId, cancellationToken);
        if (IsTerminal(entity.Status)) return;
        if (entity.IsCancellationRequested)
        {
            await SetTerminalAsync(entity, "Canceled", "Canceled", null, cancellationToken);
            return;
        }
        entity.ImpactJson = impactJson;
        await SetTerminalAsync(entity, "Succeeded", "Completed", null, cancellationToken);
    }

    public async Task FailAsync(string operationId, string errorMessage, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(operationId, cancellationToken);
        if (IsTerminal(entity.Status)) return;
        await SetTerminalAsync(entity, entity.IsCancellationRequested ? "Canceled" : "Failed", entity.IsCancellationRequested ? "Canceled" : "Failed", entity.IsCancellationRequested ? null : Truncate(errorMessage), cancellationToken);
    }

    public async Task FailRunningExceptAsync(string operationId, string errorMessage, CancellationToken cancellationToken = default)
    {
        var db = databaseAccessor.GetProjectManagementDb();
        var rows = await db.Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.Id != operationId && item.TenantId == Tenant() && item.AppCode == App() && (item.Status == "Pending" || item.Status == "Running") && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.Status = "Failed";
            row.Phase = "Failed";
            row.ErrorMessage = Truncate(errorMessage);
            row.CompletedTime = DateTime.UtcNow;
            row.UpdatedBy = UserId();
            row.UpdatedTime = row.CompletedTime;
            await PersistAsync(row, cancellationToken);
        }
    }

    private async Task SetTerminalAsync(ProjectManagementOperationEntity entity, string status, string phase, string? errorMessage, CancellationToken cancellationToken)
    {
        entity.Status = status;
        entity.Phase = phase;
        entity.ProgressPercent = status == "Succeeded" ? 100 : entity.ProgressPercent;
        entity.ErrorMessage = errorMessage;
        entity.CompletedTime = DateTime.UtcNow;
        entity.UpdatedBy = UserId();
        entity.UpdatedTime = entity.CompletedTime;
        await PersistAsync(entity, cancellationToken);
    }

    private async Task<bool> PersistAsync(ProjectManagementOperationEntity entity, CancellationToken cancellationToken)
    {
        if (transitionObserver is not null) await transitionObserver.BeforePersistAsync(entity, cancellationToken);
        var db = databaseAccessor.GetProjectManagementDb();
        var expectedVersion = entity.VersionNo;
        entity.VersionNo++;
        try
        {
            await ProjectManagementMutationTransaction.RunAsync(db, async () =>
            {
                var affected = await db.Updateable(entity)
                    .Where(item => item.Id == entity.Id && item.TenantId == entity.TenantId && item.AppCode == entity.AppCode && item.ActorUserId == entity.ActorUserId && item.VersionNo == expectedVersion && !item.IsDeleted)
                    .ExecuteCommandAsync(cancellationToken);
                if (affected != 1) throw new OperationStateChangedException();
                await InsertEventAsync(db, entity, cancellationToken);
            });
        }
        catch (OperationStateChangedException)
        {
            entity.VersionNo = expectedVersion;
            var current = await FindOwnedAsync(entity.Id, cancellationToken);
            if (current?.IsCancellationRequested == true && !IsTerminal(current.Status)) await CancelAsync(current.Id, cancellationToken);
            return false;
        }

        await PublishBestEffortAsync(entity, cancellationToken);
        return true;
    }

    private async Task CreateAndPublishAsync(ProjectManagementOperationEntity entity, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetProjectManagementDb();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            await InsertEventAsync(db, entity, cancellationToken);
        });
        await PublishBestEffortAsync(entity, cancellationToken);
    }

    private Task InsertEventAsync(ISqlSugarClient db, ProjectManagementOperationEntity entity, CancellationToken cancellationToken) =>
        db.Insertable(new ProjectManagementOperationEventEntity
        {
            TenantId = entity.TenantId,
            AppCode = entity.AppCode,
            OperationId = entity.Id,
            Status = entity.Status,
            Phase = entity.Phase,
            ProgressPercent = entity.ProgressPercent,
            IsCancellationRequested = entity.IsCancellationRequested,
            TraceId = entity.TraceId,
            ActorUserId = entity.ActorUserId,
            CreatedBy = UserId()
        }).ExecuteCommandAsync(cancellationToken);

    private async Task PublishBestEffortAsync(ProjectManagementOperationEntity entity, CancellationToken cancellationToken)
    {
        try { await PublishAsync(entity, cancellationToken); }
        catch { }
    }

    private async Task<ProjectManagementOperationEntity> GetOwnedAsync(string operationId, CancellationToken cancellationToken) =>
        await FindOwnedAsync(operationId, cancellationToken)
        ?? throw new ValidationException("长任务不存在或无权访问");

    private async Task<ProjectManagementOperationEntity?> FindOwnedAsync(string operationId, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.Id == operationId && item.TenantId == Tenant() && item.AppCode == App() && item.ActorUserId == UserId() && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken)).FirstOrDefault();

    private Task PublishAsync(ProjectManagementOperationEntity entity, CancellationToken cancellationToken) =>
        progressPublisher?.PublishAsync(entity.TenantId, entity.AppCode, entity.ActorUserId,
            new ProjectManagementOperationProgressEvent(entity.Id, entity.OperationType, entity.Status, entity.Phase, entity.ProgressPercent, entity.IsCancellationRequested, entity.CompletedTime), cancellationToken)
        ?? Task.CompletedTask;

    private static bool IsTerminal(string status) => status is "Succeeded" or "Failed" or "Canceled";
    private sealed class OperationStateChangedException : Exception;
    private static string Truncate(string value) => value.Length > 2000 ? value[..2000] : value;
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string UserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
}

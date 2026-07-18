using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementOperationWriter(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser) : IProjectManagementOperationWriter
{
    public async Task StartAsync(string operationId, string operationType, string impactJson, string traceId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var db = databaseAccessor.GetCurrentDb();
        var existing = (await db.Queryable<ProjectManagementOperationEntity>().Where(item => item.Id == operationId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
        if (existing is not null)
        {
            existing.Status = "Running";
            existing.OperationType = operationType;
            existing.ImpactJson = impactJson;
            existing.TraceId = traceId;
            existing.ErrorMessage = null;
            existing.CompletedTime = null;
            existing.UpdatedBy = UserId();
            existing.UpdatedTime = now;
            await db.Updateable(existing).ExecuteCommandAsync(cancellationToken);
            return;
        }
        await db.Insertable(new ProjectManagementOperationEntity
        {
            Id = operationId, TenantId = Tenant(), AppCode = App(), OperationType = operationType,
            Status = "Running", ImpactJson = impactJson, TraceId = traceId, ActorUserId = UserId(),
            StartedTime = now, CreatedBy = UserId(), CreatedTime = now
        }).ExecuteCommandAsync(cancellationToken);
    }

    public Task SucceedAsync(string operationId, CancellationToken cancellationToken = default) => UpdateAsync(operationId, "Succeeded", null, cancellationToken);

    public Task FailAsync(string operationId, string errorMessage, CancellationToken cancellationToken = default) => UpdateAsync(operationId, "Failed", errorMessage.Length > 2000 ? errorMessage[..2000] : errorMessage, cancellationToken);

    public async Task FailRunningExceptAsync(string operationId, string errorMessage, CancellationToken cancellationToken = default)
    {
        var db = databaseAccessor.GetCurrentDb();
        var rows = await db.Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.Id != operationId && item.Status == "Running" && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            row.Status = "Failed";
            row.ErrorMessage = errorMessage.Length > 2000 ? errorMessage[..2000] : errorMessage;
            row.CompletedTime = DateTime.UtcNow;
            row.UpdatedBy = UserId();
            row.UpdatedTime = row.CompletedTime;
        }
        if (rows.Count > 0) await db.Updateable(rows).ExecuteCommandAsync(cancellationToken);
    }

    private async Task UpdateAsync(string operationId, string status, string? errorMessage, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetCurrentDb();
        var rows = await db.Queryable<ProjectManagementOperationEntity>().Where(item => item.Id == operationId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        var entity = rows.FirstOrDefault() ?? throw new ValidationException("高风险操作记录不存在");
        entity.Status = status;
        entity.ErrorMessage = errorMessage;
        entity.CompletedTime = DateTime.UtcNow;
        entity.UpdatedBy = UserId();
        entity.UpdatedTime = entity.CompletedTime;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string UserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
}

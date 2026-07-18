using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementOperationService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementOperationWriter operationWriter,
    IBackgroundJobManager backgroundJobManager) : IProjectManagementOperationService
{
    public async Task<ProjectManagementOperationResponse> GetAsync(string operationId, CancellationToken cancellationToken = default)
    {
        RequireSystemWorkspace();
        return Map(await GetOwnedAsync(operationId, cancellationToken));
    }

    public async Task<ProjectManagementOperationResponse> RequestCancellationAsync(string operationId, CancellationToken cancellationToken = default)
    {
        RequireSystemWorkspace();
        await operationWriter.RequestCancellationAsync(Required(operationId), cancellationToken);
        return await GetAsync(operationId, cancellationToken);
    }

    public async Task<ProjectManagementOperationResponse> RunWorkspaceValidationAsync(CancellationToken cancellationToken = default)
    {
        RequireSystemWorkspace();
        var operationId = Guid.NewGuid().ToString("N");
        var traceId = global::System.Diagnostics.Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        await operationWriter.CreatePendingAsync(operationId, "maintenance.workspace-validation", "{}", traceId, cancellationToken);
        try
        {
            await backgroundJobManager.EnqueueAsync(new ProjectManagementOperationJobArgs(operationId, Tenant(), App(), UserId(), traceId));
        }
        catch (Exception exception)
        {
            await operationWriter.FailAsync(operationId, $"长任务入队失败：{exception.Message}", CancellationToken.None);
            throw;
        }

        return await GetAsync(operationId, cancellationToken);
    }

    private async Task<ProjectManagementOperationEntity> GetOwnedAsync(string operationId, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.Id == Required(operationId) && item.TenantId == Tenant() && item.AppCode == App() && item.ActorUserId == UserId() && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
        ?? throw new NotFoundException("长任务不存在或无权访问", ErrorCodes.PlatformResourceNotFound);

    private static ProjectManagementOperationResponse Map(ProjectManagementOperationEntity entity) => new(
        entity.Id, entity.OperationType, entity.Status, entity.Phase, entity.ProgressPercent, entity.IsCancellationRequested,
        entity.ImpactJson, entity.ErrorMessage, entity.TraceId, entity.StartedTime, entity.CompletedTime);

    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string UserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private void RequireSystemWorkspace() => ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
    private static string Required(string? value) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException("长任务标识不能为空") : value.Trim();
}

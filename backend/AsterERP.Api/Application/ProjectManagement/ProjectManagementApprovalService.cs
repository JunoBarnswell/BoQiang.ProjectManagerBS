using System.Globalization;
using AsterERP.Api.Application.Workflows;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementApprovalService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy accessPolicy,
    IWorkflowInstanceAppService workflowInstanceService,
    IWorkflowTaskAppService workflowTaskService,
    IProjectManagementActivityWriter activityWriter) : IProjectManagementApprovalService
{
    private const string MenuCode = "project-management";

    public async Task<ProjectManagementApprovalStateResponse> GetAsync(string entityType, string entityId, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        var target = await LoadTargetAsync(entityType, entityId, cancellationToken);
        await accessPolicy.EnsureCanViewProjectAsync(target.ProjectId, cancellationToken);
        var key = ProjectManagementApprovalKey.Build(entityType, entityId, idempotencyKey ?? "manual");
        var instance = await LoadInstanceAsync(entityType, key, cancellationToken);
        return await MapAsync(instance, target, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowTimelineItemResponse>> GetHistoryAsync(string entityType, string entityId, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        (await GetAsync(entityType, entityId, idempotencyKey, cancellationToken)).History;

    public Task<ProjectManagementApprovalStateResponse> CompleteTaskAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default) =>
        CompleteTaskCoreAsync(taskId, request, false, cancellationToken);

    public Task<ProjectManagementApprovalStateResponse> RejectTaskAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default) =>
        CompleteTaskCoreAsync(taskId, request, true, cancellationToken);

    public async Task<ProjectManagementApprovalStateResponse> WithdrawAsync(string entityType, string entityId, string? idempotencyKey, string? reason, CancellationToken cancellationToken = default)
    {
        var target = await LoadTargetAsync(entityType, entityId, cancellationToken);
        var key = ProjectManagementApprovalKey.Build(entityType, entityId, idempotencyKey ?? "manual");
        var instance = await LoadInstanceAsync(entityType, key, cancellationToken);
        if (!string.Equals(instance.StartedBy, User(), StringComparison.OrdinalIgnoreCase))
        {
            await accessPolicy.EnsureCanManageProjectAsync(target.ProjectId, cancellationToken);
        }
        await EnsureVersionAsync(instance, target, cancellationToken);
        await workflowInstanceService.WithdrawAsync(instance.ProcessInstanceId, reason, cancellationToken);
        return await SyncAsync(instance, target, "approval.withdrawn", instance.ProcessInstanceId, cancellationToken);
    }

    private async Task<ProjectManagementApprovalStateResponse> CompleteTaskCoreAsync(string taskId, WorkflowTaskActionRequest request, bool rejected, CancellationToken cancellationToken)
    {
        var taskDetail = await workflowTaskService.GetDetailAsync(taskId, cancellationToken);
        var processInstanceId = taskDetail.Task.ProcessInstanceId ?? throw new ValidationException("审批任务未绑定流程实例");
        var instance = await LoadInstanceByProcessAsync(processInstanceId, cancellationToken);
        if (!ProjectManagementApprovalKey.TryParse(instance.BusinessKey, out var entityType, out var entityId, out _)) throw new ValidationException("流程不是项目管理审批");
        var target = await LoadTargetAsync(entityType, entityId, cancellationToken);
        await EnsureVersionAsync(instance, target, cancellationToken);
        if (rejected) await workflowTaskService.RejectAsync(taskId, request, cancellationToken);
        else await workflowTaskService.CompleteAsync(taskId, request, cancellationToken);
        return await SyncAsync(instance, target, rejected ? "approval.task.rejected" : "approval.task.completed", taskId, cancellationToken);
    }

    private async Task<ProjectManagementApprovalStateResponse> SyncAsync(WorkflowBusinessInstanceEntity previous, Target target, string activityType, string traceId, CancellationToken cancellationToken)
    {
        var current = await LoadInstanceByProcessAsync(previous.ProcessInstanceId, cancellationToken);
        var db = databaseAccessor.GetProjectManagementDb();
        if (!await db.Queryable<ProjectManagementActivityEntity>().AnyAsync(item => item.TenantId == Tenant() && item.AppCode == App() && item.AggregateType == target.EntityType && item.AggregateId == target.Id && item.ActivityType == activityType && item.TraceId == traceId, cancellationToken))
        {
            await activityWriter.AppendAsync(new ProjectManagementActivityEvent(Tenant(), App(), target.EntityType, target.Id, activityType, "同步审批状态", traceId, User(), target.ProjectId), cancellationToken);
        }
        var terminalActivity = current.Status switch
        {
            "Completed" => "approval.completed",
            "Withdrawn" => "approval.withdrawn",
            "Terminated" => "approval.rejected",
            _ => null
        };
        if (terminalActivity is not null && !await db.Queryable<ProjectManagementActivityEntity>().AnyAsync(item => item.TenantId == Tenant() && item.AppCode == App() && item.AggregateType == target.EntityType && item.AggregateId == target.Id && item.ActivityType == terminalActivity && item.TraceId == current.ProcessInstanceId, cancellationToken))
        {
            await activityWriter.AppendAsync(new ProjectManagementActivityEvent(Tenant(), App(), target.EntityType, target.Id, terminalActivity, "审批流程状态同步", current.ProcessInstanceId, User(), target.ProjectId), cancellationToken);
        }
        return await MapAsync(current, target, cancellationToken);
    }

    private async Task<ProjectManagementApprovalStateResponse> MapAsync(WorkflowBusinessInstanceEntity instance, Target target, CancellationToken cancellationToken)
    {
        var detail = await workflowInstanceService.GetDetailAsync(instance.ProcessInstanceId, cancellationToken);
        var startedVersion = ReadVersion(detail.Variables);
        var approvalStatus = instance.Status switch
        {
            "Completed" => "Approved",
            "Withdrawn" => "Withdrawn",
            "Terminated" => "Rejected",
            _ when string.Equals(ReadString(detail.Variables, "approvalAction"), "reject", StringComparison.OrdinalIgnoreCase) => "Rejected",
            _ => "Pending"
        };
        var canWithdraw = string.Equals(instance.StartedBy, User(), StringComparison.OrdinalIgnoreCase) || await CanManageAsync(target.ProjectId, cancellationToken);
        return new(target.EntityType, target.Id, instance.BusinessKey, instance.ProcessInstanceId, instance.Status, approvalStatus, target.VersionNo, startedVersion, canWithdraw, (await FindBindingAsync(target.EntityType, cancellationToken))?.DetailRoute, detail.Timeline);
    }

    private async Task EnsureVersionAsync(WorkflowBusinessInstanceEntity instance, Target target, CancellationToken cancellationToken)
    {
        var detail = await workflowInstanceService.GetDetailAsync(instance.ProcessInstanceId, cancellationToken);
        var startedVersion = ReadVersion(detail.Variables);
        if (startedVersion <= 0 || target.VersionNo != startedVersion) throw new ValidationException("审批对象已被修改，请刷新后重新发起审批", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
        if (instance.Status is "Completed" or "Withdrawn" or "Terminated") throw new ValidationException("审批流程已结束，不能重复操作");
    }

    private async Task<Target> LoadTargetAsync(string entityType, string entityId, CancellationToken cancellationToken)
    {
        entityType = NormalizeEntityType(entityType);
        var db = databaseAccessor.GetProjectManagementDb();
        if (entityType == ProjectManagementAutomationEntityTypes.Project)
        {
            var row = await db.Queryable<ProjectManagementProjectEntity>().FirstAsync(item => item.Id == entityId && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted, cancellationToken) ?? throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
            return new(entityType, row.Id, row.Id, row.VersionNo);
        }
        if (entityType == ProjectManagementAutomationEntityTypes.Task)
        {
            var row = await db.Queryable<ProjectManagementTaskEntity>().FirstAsync(item => item.Id == entityId && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted, cancellationToken) ?? throw new NotFoundException("任务不存在", ErrorCodes.PlatformResourceNotFound);
            return new(entityType, row.Id, row.ProjectId, row.VersionNo);
        }
        var milestone = await db.Queryable<ProjectManagementMilestoneEntity>().FirstAsync(item => item.Id == entityId && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted, cancellationToken) ?? throw new NotFoundException("里程碑不存在", ErrorCodes.PlatformResourceNotFound);
        return new(entityType, milestone.Id, milestone.ProjectId, milestone.VersionNo);
    }

    private async Task<WorkflowBusinessInstanceEntity> LoadInstanceAsync(string entityType, string businessKey, CancellationToken cancellationToken) =>
        await databaseAccessor.GetCurrentDb().Queryable<WorkflowBusinessInstanceEntity>().Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.MenuCode == MenuCode && item.BusinessType == NormalizeEntityType(entityType) && item.BusinessKey == businessKey && !item.IsDeleted).OrderBy(item => item.StartedAt, OrderByType.Desc).FirstAsync(cancellationToken)
        ?? throw new NotFoundException("项目审批流程不存在", ErrorCodes.WorkflowInstanceNotFound);

    private async Task<WorkflowBusinessInstanceEntity> LoadInstanceByProcessAsync(string processInstanceId, CancellationToken cancellationToken) =>
        await databaseAccessor.GetCurrentDb().Queryable<WorkflowBusinessInstanceEntity>().FirstAsync(item => item.ProcessInstanceId == processInstanceId && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted, cancellationToken)
        ?? throw new NotFoundException("流程实例不存在", ErrorCodes.WorkflowInstanceNotFound);

    private async Task<WorkflowBindingEntity?> FindBindingAsync(string entityType, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetCurrentDb().Queryable<WorkflowBindingEntity>().Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.MenuCode == MenuCode && item.BusinessType == entityType && !item.IsDeleted).OrderBy(item => item.UpdatedTime, OrderByType.Desc).Take(1).ToListAsync(cancellationToken)).FirstOrDefault();

    private async Task<bool> CanManageAsync(string projectId, CancellationToken cancellationToken)
    {
        try { await accessPolicy.EnsureCanManageProjectAsync(projectId, cancellationToken); return true; }
        catch (ValidationException) { return false; }
    }

    private static string NormalizeEntityType(string value) => ProjectManagementAutomationEntityTypes.IsSupported(value.Trim()) ? value.Trim() : throw new ValidationException("审批对象类型不受支持");
    private static long ReadVersion(IReadOnlyDictionary<string, object?> variables) => variables.TryGetValue("projectManagementVersionNo", out var value) && long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var version) ? version : 0;
    private static string? ReadString(IReadOnlyDictionary<string, object?> variables, string name) => variables.TryGetValue(name, out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private sealed record Target(string EntityType, string Id, string ProjectId, long VersionNo);
}

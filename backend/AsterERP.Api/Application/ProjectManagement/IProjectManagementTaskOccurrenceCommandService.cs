using AsterERP.Contracts.ProjectManagement;
using AsterERP.Api.Modules.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 任务聚合提供给重复规则聚合的内部命令边界。
/// 实现必须复用任务创建、更新、删除的权限、活动、同步、事务和乐观并发语义；
/// 禁止重复规则服务直接写入 pm_tasks。
/// </summary>
public sealed class ProjectManagementTaskOccurrenceCapability
{
    internal static readonly ProjectManagementTaskOccurrenceCapability Instance = new();
    internal ProjectManagementTaskOccurrenceCapability() { }
}

public interface IProjectManagementTaskOccurrenceCommandService
{
    Task<ProjectManagementTaskRecurrenceOccurrenceEntity> CreateOccurrenceAsync(
        ProjectManagementTaskOccurrenceCapability capability,
        ProjectManagementTaskRecurrenceEntity recurrence,
        ProjectManagementTaskRecurrenceOccurrenceEntity occurrence,
        ProjectManagementTaskUpsertRequest task,
        CancellationToken cancellationToken = default);

    Task UpdateFutureAsync(
        ProjectManagementTaskOccurrenceCapability capability,
        ProjectManagementTaskRecurrenceEntity recurrence,
        IReadOnlyList<ProjectManagementTaskRecurrenceOccurrenceEntity> occurrences,
        ProjectManagementTaskUpsertRequest task,
        CancellationToken cancellationToken = default);

    Task DeleteFutureAsync(
        ProjectManagementTaskOccurrenceCapability capability,
        ProjectManagementTaskRecurrenceEntity recurrence,
        IReadOnlyList<ProjectManagementTaskRecurrenceOccurrenceEntity> occurrences,
        CancellationToken cancellationToken = default);
}

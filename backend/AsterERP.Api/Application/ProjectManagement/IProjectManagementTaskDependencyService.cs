using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementTaskDependencyService
{
    Task<IReadOnlyList<ProjectManagementTaskDependencyResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskDependencyResponse> CreateAsync(string projectId, ProjectManagementTaskDependencyUpsertRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectManagementTaskDependencyResponse>> CreateBatchAsync(string projectId, ProjectManagementTaskDependencyBatchCreateRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, string id, long versionNo, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskDependencyForceStartResponse> ForceStartAsync(string projectId, string taskId, ProjectManagementTaskDependencyForceStartRequest request, CancellationToken cancellationToken = default);
    Task RefreshBlockedStatesAsync(string projectId, CancellationToken cancellationToken = default);
    Task<int> PurgeForTasksAsync(string projectId, IReadOnlyCollection<string> taskIds, CancellationToken cancellationToken = default);
    Task<int> PurgeDeletedTasksAsync(string projectId, IReadOnlyCollection<string> taskIds, CancellationToken cancellationToken = default);
}

public sealed class ProjectManagementTaskTemplateDependencyCapability
{
    internal static readonly ProjectManagementTaskTemplateDependencyCapability Instance = new();
    internal ProjectManagementTaskTemplateDependencyCapability() { }
}

/// <summary>仅供任务模板命令在自己已开启的事务中复用依赖图校验。</summary>
public interface IProjectManagementTaskTemplateDependencyCommandService
{
    Task<IReadOnlyList<ProjectManagementTaskDependencyResponse>> CreateBatchInTransactionAsync(ProjectManagementTaskTemplateDependencyCapability capability, string projectId, ProjectManagementTaskDependencyBatchCreateRequest request, CancellationToken cancellationToken = default);
}

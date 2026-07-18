using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementActivityService : IProjectManagementActivityWriter
{
    Task<GridPageResult<ProjectManagementActivityResponse>> QueryAsync(
        string projectId,
        ProjectManagementActivityQuery query,
        CancellationToken cancellationToken = default);

    Task<GridPageResult<ProjectManagementActivityResponse>> QueryTaskAsync(
        string taskId,
        ProjectManagementActivityQuery query,
        CancellationToken cancellationToken = default);

    // 保留给既有内部调用方；HTTP 时间线统一使用分页重载。
    Task<IReadOnlyList<ProjectManagementActivityResponse>> QueryAsync(string projectId, int limit = 100, CancellationToken cancellationToken = default);
}

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementDisplayProjectionService
{
    Task<ProjectManagementDisplayProjection> ResolveAsync(
        IEnumerable<string?> projectIds,
        IEnumerable<ProjectManagementDisplayReference> references,
        IEnumerable<string?> userIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> FindUserIdsAsync(string keyword, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> FindProjectIdsAsync(string keyword, CancellationToken cancellationToken = default);
}

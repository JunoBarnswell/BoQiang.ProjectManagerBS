using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementImConversationService
{
    Task<ProjectManagementImConversationResponse?> GetAsync(string projectId, string? taskId, CancellationToken cancellationToken = default);

    Task<ProjectManagementImConversationResponse> EnsureAsync(string projectId, ProjectManagementImConversationEnsureRequest request, CancellationToken cancellationToken = default);

    Task<ProjectManagementImConversationTargetResponse> ResolveTargetAsync(string conversationId, CancellationToken cancellationToken = default);

    Task SynchronizeProjectLinksAsync(string projectId, CancellationToken cancellationToken = default);

    Task RevokeProjectMemberAsync(string projectId, string userId, CancellationToken cancellationToken = default);

    Task RevokeTaskParticipantAsync(string taskId, string userId, CancellationToken cancellationToken = default);

    Task SynchronizeTaskLinksAsync(string taskId, CancellationToken cancellationToken = default);

    Task ArchiveProjectLinksAsync(string projectId, CancellationToken cancellationToken = default);

    Task ArchiveTaskLinksAsync(IReadOnlyCollection<string> taskIds, CancellationToken cancellationToken = default);

    Task ReactivateProjectLinksAsync(string projectId, CancellationToken cancellationToken = default);

    Task ReactivateTaskLinksAsync(IReadOnlyCollection<string> taskIds, CancellationToken cancellationToken = default);
}

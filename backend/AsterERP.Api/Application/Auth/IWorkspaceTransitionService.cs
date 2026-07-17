using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.Auth;

namespace AsterERP.Api.Application.Auth;

public interface IWorkspaceTransitionService
{
    Task<SystemUserEntity> ResolveCurrentUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<SystemUserEntity> ResolveCurrentUserAsync(
        string userId,
        string? tenantId,
        string? appCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceResponse>> GetAvailableWorkspacesAsync(string userId, CancellationToken cancellationToken = default);

    Task<WorkspaceSessionSnapshot> BuildCurrentSessionAsync(
        SystemUserEntity user,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default);

    Task<ResolvedWorkspace> ResolveWorkspaceForUserAsync(
        string userId,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSessionSnapshot> BuildWorkspaceSessionAsync(
        SystemUserEntity user,
        ResolvedWorkspace workspace,
        IReadOnlyList<WorkspaceResponse> availableWorkspaces,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSessionSnapshot> SwitchAsync(
        SystemUserEntity user,
        string tenantId,
        string appCode,
        string? authorizationHeader,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSessionSnapshot> EnterApplicationBackendAsync(
        SystemUserEntity user,
        string tenantId,
        string appCode,
        string? authorizationHeader,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSessionSnapshot> SwitchPlatformAsync(
        SystemUserEntity user,
        string? authorizationHeader,
        CancellationToken cancellationToken = default);
}

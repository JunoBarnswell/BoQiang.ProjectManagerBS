namespace AsterERP.Contracts.Auth;

public sealed record LoginResponse(
    string AccessToken,
    CurrentUserResponse User,
    IReadOnlyList<WorkspaceResponse> AvailableWorkspaces,
    CurrentWorkspaceResponse? CurrentWorkspace);

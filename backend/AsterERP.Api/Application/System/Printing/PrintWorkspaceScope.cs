namespace AsterERP.Api.Application.System.Printing;

public sealed record PrintWorkspaceScope(
    string TenantId,
    string AppCode,
    string UserId,
    string UserName);

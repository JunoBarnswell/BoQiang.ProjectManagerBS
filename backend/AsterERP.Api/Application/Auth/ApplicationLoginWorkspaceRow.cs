using AsterERP.Api.Modules.Platform;

namespace AsterERP.Api.Application.Auth;

public sealed class ApplicationLoginWorkspaceRow
{
    public required SystemTenantAppEntity TenantApp { get; init; }

    public required SystemTenantEntity Tenant { get; init; }

    public required SystemApplicationEntity Application { get; init; }
}

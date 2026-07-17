using AsterERP.Api.Modules.Platform;

namespace AsterERP.Api.Application.Auth;

public sealed class WorkspaceMetadataRow
{
    public required SystemTenantAppEntity TenantApp { get; init; }

    public required SystemTenantEntity Tenant { get; init; }

    public required SystemApplicationEntity Application { get; init; }
}

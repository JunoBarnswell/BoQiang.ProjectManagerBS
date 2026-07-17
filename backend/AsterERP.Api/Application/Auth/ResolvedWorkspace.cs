using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Users;

namespace AsterERP.Api.Application.Auth;

public sealed record ResolvedWorkspace(
    SystemTenantEntity Tenant,
    SystemApplicationEntity Application,
    SystemTenantAppEntity TenantApp,
    SystemUserTenantMembershipEntity Membership);

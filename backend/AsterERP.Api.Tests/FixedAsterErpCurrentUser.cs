using System.Security.Claims;
using Volo.Abp.Security.Claims;
using Volo.Abp.Users;

namespace AsterERP.Api.Tests;

internal sealed class FixedAsterErpCurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal principal;

    public FixedAsterErpCurrentUser(ClaimsPrincipal principal)
    {
        this.principal = principal;
    }

    public bool IsAuthenticated => principal.Identity?.IsAuthenticated == true;

    public Guid? Id => Guid.TryParse(FindClaim(AbpClaimTypes.UserId)?.Value, out var value) ? value : null;

    public string? UserName => FindClaim(AbpClaimTypes.UserName)?.Value;

    public string? Name => FindClaim(AbpClaimTypes.Name)?.Value;

    public string? SurName => FindClaim(AbpClaimTypes.SurName)?.Value;

    public string? PhoneNumber => FindClaim(AbpClaimTypes.PhoneNumber)?.Value;

    public bool PhoneNumberVerified => TryReadBoolean(FindClaim(AbpClaimTypes.PhoneNumberVerified)?.Value);

    public string? Email => FindClaim(AbpClaimTypes.Email)?.Value;

    public bool EmailVerified => TryReadBoolean(FindClaim(AbpClaimTypes.EmailVerified)?.Value);

    public Guid? TenantId => Guid.TryParse(FindClaim(AbpClaimTypes.TenantId)?.Value, out var value) ? value : null;

    public string[] Roles => FindClaims(AbpClaimTypes.Role).Select(claim => claim.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public Claim? FindClaim(string claimType) => principal.FindFirst(claimType);

    public Claim[] FindClaims(string claimType) => principal.FindAll(claimType).ToArray();

    public Claim[] GetAllClaims() => principal.Claims.ToArray();

    public bool IsInRole(string roleName) => Roles.Contains(roleName, StringComparer.OrdinalIgnoreCase);

    private static bool TryReadBoolean(string? value) => bool.TryParse(value, out var result) && result;
}

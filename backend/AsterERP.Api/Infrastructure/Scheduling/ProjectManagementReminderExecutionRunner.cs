using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Shared;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Security.Claims;

namespace AsterERP.Api.Infrastructure.Scheduling;

public sealed class ProjectManagementReminderExecutionRunner(
    IHttpContextAccessor httpContextAccessor,
    IDataPermissionFilterRegistrar dataPermissionFilterRegistrar,
    ProjectManagementReminderExecutionService executionService)
{
    public async Task ExecuteAsync(ProjectManagementReminderJobArgs args, CancellationToken cancellationToken)
    {
        var previousContext = httpContextAccessor.HttpContext;
        httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = CreateReminderPrincipal(args)
        };
        try
        {
            using var filterScope = await dataPermissionFilterRegistrar.RegisterAsync(cancellationToken);
            await executionService.ExecuteAsync(args, cancellationToken);
        }
        finally
        {
            httpContextAccessor.HttpContext = previousContext;
        }
    }

    private static ClaimsPrincipal CreateReminderPrincipal(ProjectManagementReminderJobArgs args)
    {
        var claims = new[]
        {
            new Claim(AbpClaimTypes.UserId, args.RecipientUserId),
            new Claim(AbpClaimTypes.UserName, args.RecipientUserId),
            new Claim(AsterErpClaimTypes.UserId, args.RecipientUserId),
            new Claim(AsterErpClaimTypes.TenantId, args.TenantId),
            new Claim(AsterErpClaimTypes.AppCode, args.AppCode),
            new Claim(AsterErpClaimTypes.DataScope, "SELF"),
            new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementTaskView),
            new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementReminderView)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, AsterErpClaimsPrincipalFactory.AuthenticationType, AbpClaimTypes.UserName, AbpClaimTypes.Role));
    }
}

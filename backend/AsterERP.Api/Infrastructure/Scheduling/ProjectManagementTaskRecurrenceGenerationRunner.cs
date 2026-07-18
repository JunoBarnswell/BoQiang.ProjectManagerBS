using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Shared;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Security.Claims;

namespace AsterERP.Api.Infrastructure.Scheduling;

/// <summary>以系列创建者的受限权限快照执行作业；不把 Hangfire 伪装成拥有绕过权限的系统用户。</summary>
public sealed class ProjectManagementTaskRecurrenceGenerationRunner(
    IHttpContextAccessor httpContextAccessor,
    IDataPermissionFilterRegistrar dataPermissionFilterRegistrar,
    IProjectManagementTaskRecurrenceService recurrenceService)
{
    public async Task ExecuteAsync(ProjectManagementTaskRecurrenceGenerationJobArgs args, CancellationToken cancellationToken)
    {
        var previousContext = httpContextAccessor.HttpContext;
        httpContextAccessor.HttpContext = new DefaultHttpContext { User = CreatePrincipal(args) };
        try
        {
            using var filterScope = await dataPermissionFilterRegistrar.RegisterAsync(cancellationToken);
            await recurrenceService.GenerateAsync(args, cancellationToken);
        }
        finally
        {
            httpContextAccessor.HttpContext = previousContext;
        }
    }

    private static ClaimsPrincipal CreatePrincipal(ProjectManagementTaskRecurrenceGenerationJobArgs args)
    {
        var claims = new[]
        {
            new Claim(AbpClaimTypes.UserId, args.SeriesOwnerUserId), new Claim(AbpClaimTypes.UserName, args.SeriesOwnerUserId),
            new Claim(AsterErpClaimTypes.UserId, args.SeriesOwnerUserId), new Claim(AsterErpClaimTypes.TenantId, args.TenantId),
            new Claim(AsterErpClaimTypes.AppCode, args.AppCode), new Claim(AsterErpClaimTypes.DataScope, "SELF"),
            new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementTaskView),
            new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementTaskAdd),
            new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementTaskEdit),
            new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementTaskDelete)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, AsterErpClaimsPrincipalFactory.AuthenticationType, AbpClaimTypes.UserName, AbpClaimTypes.Role));
    }
}

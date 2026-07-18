using System.Security.Claims;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Shared;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Security.Claims;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementOperationRunner(
    IHttpContextAccessor httpContextAccessor,
    IDataPermissionFilterRegistrar dataPermissionFilterRegistrar,
    ProjectManagementWorkspaceValidationExecutor workspaceValidationExecutor,
    IProjectManagementSearchService? searchService = null,
    ProjectManagementReportSnapshotExecutor? reportSnapshotExecutor = null,
    ProjectManagementPurgeFileDeletionExecutor? purgeFileDeletionExecutor = null)
{
    public async Task ExecuteAsync(ProjectManagementOperationJobArgs args)
    {
        var previousContext = httpContextAccessor.HttpContext;
        httpContextAccessor.HttpContext = new DefaultHttpContext { User = CreatePrincipal(args) };
        httpContextAccessor.HttpContext.Request.Path = "/api/project-management/operations/maintenance/workspace-validation";
        try
        {
            using var filterScope = await dataPermissionFilterRegistrar.RegisterAsync(CancellationToken.None);
            if (searchService is not null && await searchService.TryExecuteIndexOperationAsync(args, CancellationToken.None)) return;
            if (reportSnapshotExecutor is not null && await reportSnapshotExecutor.TryExecuteAsync(args, CancellationToken.None)) return;
            if (purgeFileDeletionExecutor is not null && await purgeFileDeletionExecutor.TryExecuteAsync(args, CancellationToken.None)) return;
            await workspaceValidationExecutor.ExecuteAsync(args, CancellationToken.None);
        }
        finally
        {
            httpContextAccessor.HttpContext = previousContext;
        }
    }

    private static ClaimsPrincipal CreatePrincipal(ProjectManagementOperationJobArgs args)
    {
        var claims = new[]
        {
            new Claim(AbpClaimTypes.UserId, args.ActorUserId),
            new Claim(AbpClaimTypes.UserName, args.ActorUserId),
            new Claim(AsterErpClaimTypes.UserId, args.ActorUserId),
            new Claim(AsterErpClaimTypes.TenantId, args.TenantId),
            new Claim(AsterErpClaimTypes.AppCode, args.AppCode),
            new Claim(AsterErpClaimTypes.DataScope, "SELF"),
            new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementReportExport),
            new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementOperationView),
            new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementOperationManage)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, AsterErpClaimsPrincipalFactory.AuthenticationType, AbpClaimTypes.UserName, AbpClaimTypes.Role));
    }
}

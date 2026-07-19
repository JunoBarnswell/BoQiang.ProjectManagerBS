using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Shared;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Security.Claims;

namespace AsterERP.Api.Infrastructure.Scheduling;

public sealed class ProjectManagementWebhookDeliveryRunner(IHttpContextAccessor httpContextAccessor, IDataPermissionFilterRegistrar dataPermissionFilterRegistrar, IProjectManagementWebhookService service)
{
    public async Task ExecuteAsync(ProjectManagementWebhookDeliveryJobArgs args, CancellationToken cancellationToken)
    {
        var previous = httpContextAccessor.HttpContext;
        httpContextAccessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AbpClaimTypes.UserId, args.ActorUserId), new Claim(AsterErpClaimTypes.UserId, args.ActorUserId), new Claim(AsterErpClaimTypes.TenantId, args.TenantId), new Claim(AsterErpClaimTypes.AppCode, args.AppCode), new Claim(AsterErpClaimTypes.DataScope, "SELF"), new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementProjectView)], AsterErpClaimsPrincipalFactory.AuthenticationType)) };
        try { using var scope = await dataPermissionFilterRegistrar.RegisterAsync(cancellationToken); await service.DeliverAsync(args, cancellationToken); }
        finally { httpContextAccessor.HttpContext = previous; }
    }
}

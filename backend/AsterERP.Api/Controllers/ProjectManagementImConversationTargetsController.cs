using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

/// <summary>
/// IM 只持有会话标识；由项目域根据该标识解析关联对象并重新执行对象级授权。
/// </summary>
[Route("api/project-management/im-conversations")]
[Permission(PermissionCodes.ProjectManagementImConversationView)]
public sealed class ProjectManagementImConversationTargetsController(IProjectManagementImConversationService service) : BaseApiController
{
    [HttpGet("{conversationId}/target")]
    public async Task<IActionResult> ResolveTargetAsync(string conversationId, CancellationToken cancellationToken) =>
        ApiOk(await service.ResolveTargetAsync(conversationId, cancellationToken));
}

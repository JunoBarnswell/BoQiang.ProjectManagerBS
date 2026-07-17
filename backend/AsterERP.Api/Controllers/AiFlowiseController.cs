using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/flowise")]
public sealed class AiFlowiseController(
    IFlowiseManagementService managementService,
    IFlowiseCanvasService canvasService,
    IFlowiseExecutionService executionService) : BaseApiController
{
    [HttpGet("overview")]
    [Permission(PermissionCodes.FlowiseView)]
    public async Task<IActionResult> GetOverviewAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await managementService.GetOverviewAsync(cancellationToken));
    }

    [HttpGet("resource-types")]
    [Permission(PermissionCodes.FlowiseView)]
    public async Task<IActionResult> GetResourceTypesAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await managementService.GetResourceTypesAsync(cancellationToken));
    }

    [HttpGet("workspaces")]
    [Permission(PermissionCodes.FlowiseWorkspacesView)]
    public async Task<IActionResult> GetWorkspacesAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await managementService.GetWorkspacesAsync(query, cancellationToken));
    }

    [HttpPost("workspaces")]
    [Permission(PermissionCodes.FlowiseWorkspacesManage)]
    public async Task<IActionResult> CreateWorkspaceAsync([FromBody] FlowiseWorkspaceUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await managementService.UpsertWorkspaceAsync(null, request, cancellationToken));
    }

    [HttpPut("workspaces/{id}")]
    [Permission(PermissionCodes.FlowiseWorkspacesManage)]
    public async Task<IActionResult> UpdateWorkspaceAsync(string id, [FromBody] FlowiseWorkspaceUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await managementService.UpsertWorkspaceAsync(id, request, cancellationToken));
    }

    [HttpDelete("workspaces/{id}")]
    [Permission(PermissionCodes.FlowiseWorkspacesManage)]
    public async Task<IActionResult> DeleteWorkspaceAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await managementService.DeleteWorkspaceAsync(id, cancellationToken));
    }

    [HttpGet("shared-workspaces/{itemId}")]
    [Permission(PermissionCodes.FlowiseWorkspacesView)]
    public async Task<IActionResult> GetSharedWorkspacesAsync(string itemId, CancellationToken cancellationToken)
    {
        return ApiOk(await managementService.GetSharedWorkspacesAsync(itemId, cancellationToken));
    }

    [HttpPut("shared-workspaces/{itemId}")]
    [Permission(PermissionCodes.FlowiseWorkspacesManage)]
    public async Task<IActionResult> SetSharedWorkspacesAsync(string itemId, [FromBody] FlowiseShareWorkspacesRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await managementService.SetSharedWorkspacesAsync(itemId, request, cancellationToken));
    }

    [HttpGet("canvas/nodes")]
    [Permission(PermissionCodes.FlowiseView)]
    public async Task<IActionResult> GetCanvasNodesAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await canvasService.GetNodeCatalogAsync(cancellationToken));
    }

    [HttpGet("canvas/{resourceId}")]
    [Permission(PermissionCodes.FlowiseView)]
    public async Task<IActionResult> GetCanvasAsync(string resourceId, CancellationToken cancellationToken)
    {
        return ApiOk(await canvasService.GetByResourceAsync(resourceId, cancellationToken));
    }

    [HttpPost("canvas")]
    [Permission(PermissionCodes.FlowiseEdit)]
    public async Task<IActionResult> SaveCanvasAsync([FromBody] FlowiseCanvasUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await canvasService.SaveAsync(request, cancellationToken));
    }

    [HttpPut("canvas")]
    [Permission(PermissionCodes.FlowiseEdit)]
    public async Task<IActionResult> PutCanvasAsync([FromBody] FlowiseCanvasUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await canvasService.SaveAsync(request, cancellationToken));
    }

    [HttpGet("executions")]
    [Permission(PermissionCodes.FlowiseExecutionsView)]
    public async Task<IActionResult> GetExecutionsAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await executionService.GetPageAsync(query, cancellationToken));
    }

    [HttpGet("executions/{id}")]
    [Permission(PermissionCodes.FlowiseExecutionsView)]
    public async Task<IActionResult> GetExecutionAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await executionService.GetAsync(id, cancellationToken));
    }

    [HttpPost("executions/run")]
    [Permission(PermissionCodes.FlowiseRun)]
    public async Task<IActionResult> RunExecutionAsync([FromBody] FlowiseExecutionStartRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await executionService.StartAsync(request, cancellationToken));
    }

    [HttpDelete("executions/{id}")]
    [Permission(PermissionCodes.FlowiseExecutionsManage)]
    public async Task<IActionResult> DeleteExecutionAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await executionService.DeleteAsync(id, cancellationToken));
    }

    [HttpGet("account/settings")]
    [Permission(PermissionCodes.FlowiseAccountView)]
    public async Task<IActionResult> GetAccountAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await managementService.GetAccountAsync(cancellationToken));
    }

    [HttpPut("account/settings")]
    [Permission(PermissionCodes.FlowiseAccountEdit)]
    public async Task<IActionResult> UpdateAccountAsync([FromBody] FlowiseAccountSettingsDto request, CancellationToken cancellationToken)
    {
        return ApiOk(await managementService.UpdateAccountAsync(request, cancellationToken));
    }

    [HttpGet("sso-config")]
    [Permission(PermissionCodes.FlowiseSsoManage)]
    public async Task<IActionResult> GetSsoConfigsAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken) =>
        ApiOk(await managementService.GetSsoConfigsAsync(query, cancellationToken));

    [HttpGet("sso-config/detail")]
    [Permission(PermissionCodes.FlowiseSsoManage)]
    public async Task<IActionResult> GetSsoConfigDetailAsync(CancellationToken cancellationToken) =>
        ApiOk(await managementService.GetSsoConfigDetailAsync(cancellationToken));

    [HttpPost("sso-config")]
    [Permission(PermissionCodes.FlowiseSsoManage)]
    public async Task<IActionResult> CreateSsoConfigAsync([FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await managementService.CreateSsoConfigAsync(request, cancellationToken));

    [HttpPut("sso-config/{id}")]
    [Permission(PermissionCodes.FlowiseSsoManage)]
    public async Task<IActionResult> UpdateSsoConfigAsync(string id, [FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await managementService.UpdateSsoConfigAsync(id, request, cancellationToken));

    [HttpDelete("sso-config/{id}")]
    [Permission(PermissionCodes.FlowiseSsoManage)]
    public async Task<IActionResult> DeleteSsoConfigAsync(string id, CancellationToken cancellationToken) =>
        ApiOk(await managementService.DeleteSsoConfigAsync(id, cancellationToken));

    [HttpGet("roles")]
    [Permission(PermissionCodes.FlowiseRolesManage)]
    public async Task<IActionResult> GetRolesAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken) =>
        ApiOk(await managementService.GetRolesAsync(query, cancellationToken));

    [HttpGet("roles/{id}/detail")]
    [Permission(PermissionCodes.FlowiseRolesManage)]
    public async Task<IActionResult> GetRoleDetailAsync(string id, CancellationToken cancellationToken) =>
        ApiOk(await managementService.GetRoleDetailAsync(id, cancellationToken));

    [HttpPost("roles")]
    [Permission(PermissionCodes.FlowiseRolesManage)]
    public async Task<IActionResult> CreateRoleAsync([FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await managementService.CreateRoleAsync(request, cancellationToken));

    [HttpPut("roles/{id}")]
    [Permission(PermissionCodes.FlowiseRolesManage)]
    public async Task<IActionResult> UpdateRoleAsync(string id, [FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await managementService.UpdateRoleAsync(id, request, cancellationToken));

    [HttpDelete("roles/{id}")]
    [Permission(PermissionCodes.FlowiseRolesManage)]
    public async Task<IActionResult> DeleteRoleAsync(string id, CancellationToken cancellationToken) =>
        ApiOk(await managementService.DeleteRoleAsync(id, cancellationToken));

    [HttpGet("users")]
    [Permission(PermissionCodes.FlowiseUsersManage)]
    public async Task<IActionResult> GetUsersAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken) =>
        ApiOk(await managementService.GetUsersAsync(query, cancellationToken));

    [HttpGet("users/{id}/detail")]
    [Permission(PermissionCodes.FlowiseUsersManage)]
    public async Task<IActionResult> GetUserDetailAsync(string id, CancellationToken cancellationToken) =>
        ApiOk(await managementService.GetUserDetailAsync(id, cancellationToken));

    [HttpPost("users")]
    [Permission(PermissionCodes.FlowiseUsersManage)]
    public async Task<IActionResult> CreateUserAsync([FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await managementService.CreateUserAsync(request, cancellationToken));

    [HttpPut("users/{id}")]
    [Permission(PermissionCodes.FlowiseUsersManage)]
    public async Task<IActionResult> UpdateUserAsync(string id, [FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await managementService.UpdateUserAsync(id, request, cancellationToken));

    [HttpDelete("users/{id}")]
    [Permission(PermissionCodes.FlowiseUsersManage)]
    public async Task<IActionResult> DeleteUserAsync(string id, CancellationToken cancellationToken) =>
        ApiOk(await managementService.DeleteUserAsync(id, cancellationToken));

    [HttpGet("login-activity")]
    [Permission(PermissionCodes.FlowiseLoginActivityView)]
    public async Task<IActionResult> GetLoginActivityResourcesAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken) =>
        ApiOk(await managementService.GetLoginActivityResourcesAsync(query, cancellationToken));

    [HttpGet("login-activity/detail")]
    [Permission(PermissionCodes.FlowiseLoginActivityView)]
    public async Task<IActionResult> GetLoginActivityAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken) =>
        ApiOk(await managementService.GetLoginActivityAsync(query, cancellationToken));

    [HttpPost("login-activity")]
    [Permission(PermissionCodes.FlowiseLoginActivityManage)]
    public async Task<IActionResult> CreateLoginActivityAsync([FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await managementService.CreateLoginActivityAsync(request, cancellationToken));

    [HttpPut("login-activity/{id}")]
    [Permission(PermissionCodes.FlowiseLoginActivityManage)]
    public async Task<IActionResult> UpdateLoginActivityAsync(string id, [FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await managementService.UpdateLoginActivityAsync(id, request, cancellationToken));

    [HttpDelete("login-activity/{id}")]
    [Permission(PermissionCodes.FlowiseLoginActivityManage)]
    public async Task<IActionResult> DeleteLoginActivityAsync(string id, CancellationToken cancellationToken) =>
        ApiOk(await managementService.DeleteLoginActivityAsync(id, cancellationToken));

    [HttpGet("logs")]
    [Permission(PermissionCodes.FlowiseLogsView)]
    public async Task<IActionResult> GetLogResourcesAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken) =>
        ApiOk(await managementService.GetLogResourcesAsync(query, cancellationToken));

    [HttpGet("logs/detail")]
    [Permission(PermissionCodes.FlowiseLogsRead)]
    public async Task<IActionResult> GetLogsAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken) =>
        ApiOk(await managementService.GetLogsAsync(query, cancellationToken));

    [HttpPost("logs")]
    [Permission(PermissionCodes.FlowiseLogsManage)]
    public async Task<IActionResult> CreateLogAsync([FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await managementService.CreateLogAsync(request, cancellationToken));

    [HttpPut("logs/{id}")]
    [Permission(PermissionCodes.FlowiseLogsManage)]
    public async Task<IActionResult> UpdateLogAsync(string id, [FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await managementService.UpdateLogAsync(id, request, cancellationToken));

    [HttpDelete("logs/{id}")]
    [Permission(PermissionCodes.FlowiseLogsManage)]
    public async Task<IActionResult> DeleteLogAsync(string id, CancellationToken cancellationToken) =>
        ApiOk(await managementService.DeleteLogAsync(id, cancellationToken));
}

using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/flowise")]
public sealed class AiFlowiseChatflowsController(IFlowiseChatflowService chatflowService) : BaseApiController
{
    [HttpGet("chatflows")]
    [Permission(PermissionCodes.FlowiseChatflowsView)]
    public async Task<IActionResult> GetChatflowsAsync([FromQuery] FlowiseChatflowQuery query, CancellationToken cancellationToken)
    {
        query.Type = FlowiseChatflowTypes.Chatflow;
        return ApiOk(await chatflowService.GetPageAsync(query, cancellationToken));
    }

    [HttpPost("chatflows")]
    [Permission(PermissionCodes.FlowiseChatflowsEdit)]
    public async Task<IActionResult> CreateChatflowAsync([FromBody] FlowiseChatflowUpsertRequest request, CancellationToken cancellationToken)
    {
        request.Type = FlowiseChatflowTypes.Chatflow;
        return ApiOk(await chatflowService.CreateAsync(request, cancellationToken));
    }

    [HttpGet("chatflows/{id}")]
    [Permission(PermissionCodes.FlowiseChatflowsView)]
    public async Task<IActionResult> GetChatflowAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await chatflowService.GetAsync(id, cancellationToken));
    }

    [HttpPut("chatflows/{id}")]
    [Permission(PermissionCodes.FlowiseChatflowsEdit)]
    public async Task<IActionResult> UpdateChatflowAsync(string id, [FromBody] FlowiseChatflowUpsertRequest request, CancellationToken cancellationToken)
    {
        request.Type = FlowiseChatflowTypes.Chatflow;
        return ApiOk(await chatflowService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPut("chatflows/{id}/configuration")]
    [Permission(PermissionCodes.FlowiseChatflowsConfig)]
    public async Task<IActionResult> UpdateChatflowConfigurationAsync(string id, [FromBody] FlowiseChatflowConfigurationRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await chatflowService.UpdateConfigurationAsync(id, request, cancellationToken));
    }

    [HttpPut("chatflows/{id}/domains")]
    [Permission(PermissionCodes.FlowiseChatflowsDomains)]
    public async Task<IActionResult> UpdateChatflowDomainsAsync(string id, [FromBody] FlowiseChatflowDomainsRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await chatflowService.UpdateDomainsAsync(id, request, cancellationToken));
    }

    [HttpGet("chatflows/{id}/schedule/status")]
    [Permission(PermissionCodes.FlowiseChatflowsView)]
    public async Task<IActionResult> GetChatflowScheduleStatusAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await chatflowService.GetScheduleStatusAsync(id, cancellationToken));
    }

    [HttpGet("chatflows/{id}/schedule/logs")]
    [Permission(PermissionCodes.FlowiseChatflowsView)]
    public async Task<IActionResult> GetChatflowScheduleLogsAsync(string id, [FromQuery] FlowiseScheduleLogQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await chatflowService.GetScheduleTriggerLogsAsync(id, query, cancellationToken));
    }

    [HttpDelete("chatflows/{id}")]
    [Permission(PermissionCodes.FlowiseChatflowsDelete)]
    public async Task<IActionResult> DeleteChatflowAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await chatflowService.DeleteAsync(id, cancellationToken));
    }

    [HttpGet("agentflows")]
    [Permission(PermissionCodes.FlowiseAgentflowsView)]
    public async Task<IActionResult> GetAgentflowsAsync([FromQuery] FlowiseChatflowQuery query, CancellationToken cancellationToken)
    {
        query.Type = FlowiseChatflowTypes.Agentflow;
        return ApiOk(await chatflowService.GetPageAsync(query, cancellationToken));
    }

    [HttpPost("agentflows")]
    [Permission(PermissionCodes.FlowiseAgentflowsEdit)]
    public async Task<IActionResult> CreateAgentflowAsync([FromBody] FlowiseChatflowUpsertRequest request, CancellationToken cancellationToken)
    {
        request.Type = FlowiseChatflowTypes.Agentflow;
        return ApiOk(await chatflowService.CreateAsync(request, cancellationToken));
    }

    [HttpGet("agentflows/{id}")]
    [Permission(PermissionCodes.FlowiseAgentflowsView)]
    public async Task<IActionResult> GetAgentflowAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await chatflowService.GetAsync(id, cancellationToken));
    }

    [HttpPut("agentflows/{id}")]
    [Permission(PermissionCodes.FlowiseAgentflowsEdit)]
    public async Task<IActionResult> UpdateAgentflowAsync(string id, [FromBody] FlowiseChatflowUpsertRequest request, CancellationToken cancellationToken)
    {
        request.Type = FlowiseChatflowTypes.Agentflow;
        return ApiOk(await chatflowService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPut("agentflows/{id}/configuration")]
    [Permission(PermissionCodes.FlowiseAgentflowsConfig)]
    public async Task<IActionResult> UpdateAgentflowConfigurationAsync(string id, [FromBody] FlowiseChatflowConfigurationRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await chatflowService.UpdateConfigurationAsync(id, request, cancellationToken));
    }

    [HttpPut("agentflows/{id}/domains")]
    [Permission(PermissionCodes.FlowiseAgentflowsDomains)]
    public async Task<IActionResult> UpdateAgentflowDomainsAsync(string id, [FromBody] FlowiseChatflowDomainsRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await chatflowService.UpdateDomainsAsync(id, request, cancellationToken));
    }

    [HttpGet("agentflows/{id}/schedule/status")]
    [Permission(PermissionCodes.FlowiseAgentflowsView)]
    public async Task<IActionResult> GetAgentflowScheduleStatusAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await chatflowService.GetScheduleStatusAsync(id, cancellationToken));
    }

    [HttpGet("agentflows/{id}/schedule/logs")]
    [Permission(PermissionCodes.FlowiseAgentflowsView)]
    public async Task<IActionResult> GetAgentflowScheduleLogsAsync(string id, [FromQuery] FlowiseScheduleLogQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await chatflowService.GetScheduleTriggerLogsAsync(id, query, cancellationToken));
    }

    [HttpDelete("agentflows/{id}")]
    [Permission(PermissionCodes.FlowiseAgentflowsDelete)]
    public async Task<IActionResult> DeleteAgentflowAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await chatflowService.DeleteAsync(id, cancellationToken));
    }

    [HttpPost("chatflows/validate-flow-data")]
    [Permission(PermissionCodes.FlowiseView)]
    public async Task<IActionResult> ValidateFlowDataAsync([FromBody] FlowiseChatflowUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await chatflowService.ValidateFlowDataAsync(request.FlowData, cancellationToken));
    }
}

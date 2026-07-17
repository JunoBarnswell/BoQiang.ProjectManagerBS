using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise.Assistants;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/flowise")]
public sealed class AiFlowiseResourcesController(
    IFlowiseToolService toolService,
    IFlowiseCredentialService credentialService,
    IFlowiseVariableService variableService,
    IFlowiseApiKeyService apiKeyService,
    IFlowiseAssistantService assistantService,
    IFlowiseMarketplaceService marketplaceService) : BaseApiController
{
    [HttpGet("assistants")]
    [Permission(PermissionCodes.FlowiseAssistantsView)]
    public async Task<IActionResult> GetAssistantsAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken)
        => ApiOk(await assistantService.GetPageAsync(query, cancellationToken));

    [HttpGet("assistants/{id}")]
    [Permission(PermissionCodes.FlowiseAssistantsView)]
    public async Task<IActionResult> GetAssistantAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await assistantService.GetAsync(id, cancellationToken));

    [HttpPost("assistants")]
    [Permission(PermissionCodes.FlowiseAssistantsEdit)]
    public async Task<IActionResult> CreateAssistantAsync([FromBody] FlowiseAssistantUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await assistantService.CreateAsync(request, cancellationToken));

    [HttpPut("assistants/{id}")]
    [Permission(PermissionCodes.FlowiseAssistantsEdit)]
    public async Task<IActionResult> UpdateAssistantAsync(string id, [FromBody] FlowiseAssistantUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await assistantService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("assistants/{id}")]
    [Permission(PermissionCodes.FlowiseAssistantsEdit)]
    public async Task<IActionResult> DeleteAssistantAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await assistantService.DeleteAsync(id, cancellationToken));

    [HttpGet("marketplaces")]
    [Permission(PermissionCodes.FlowiseMarketplacesView)]
    public async Task<IActionResult> GetMarketplacesAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken)
        => ApiOk(await marketplaceService.GetPageAsync(query, cancellationToken));

    [HttpGet("marketplaces/{id}")]
    [Permission(PermissionCodes.FlowiseMarketplacesView)]
    public async Task<IActionResult> GetMarketplaceAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await marketplaceService.GetAsync(id, cancellationToken));

    [HttpPost("marketplaces")]
    [Permission(PermissionCodes.FlowiseMarketplacesEdit)]
    public async Task<IActionResult> CreateMarketplaceAsync([FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await marketplaceService.CreateAsync(request, cancellationToken));

    [HttpPost("marketplaces/from-flow-template")]
    [Permission(PermissionCodes.FlowiseTemplatesFlowExport)]
    public async Task<IActionResult> CreateMarketplaceFromFlowTemplateAsync([FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await marketplaceService.CreateFromFlowTemplateAsync(request, cancellationToken));

    [HttpPut("marketplaces/{id}")]
    [Permission(PermissionCodes.FlowiseMarketplacesEdit)]
    public async Task<IActionResult> UpdateMarketplaceAsync(string id, [FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await marketplaceService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("marketplaces/{id}")]
    [Permission(PermissionCodes.FlowiseMarketplacesEdit)]
    public async Task<IActionResult> DeleteMarketplaceAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await marketplaceService.DeleteAsync(id, cancellationToken));

    [HttpGet("tools")]
    [Permission(PermissionCodes.FlowiseToolsView)]
    public async Task<IActionResult> GetToolsAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken)
        => ApiOk(await toolService.GetPageAsync(query, cancellationToken));

    [HttpGet("tools/{id}")]
    [Permission(PermissionCodes.FlowiseToolsView)]
    public async Task<IActionResult> GetToolAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await toolService.GetAsync(id, cancellationToken));

    [HttpPost("tools")]
    [Permission(PermissionCodes.FlowiseToolsEdit)]
    public async Task<IActionResult> CreateToolAsync([FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await toolService.CreateAsync(request, cancellationToken));

    [HttpPut("tools/{id}")]
    [Permission(PermissionCodes.FlowiseToolsEdit)]
    public async Task<IActionResult> UpdateToolAsync(string id, [FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await toolService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("tools/{id}")]
    [Permission(PermissionCodes.FlowiseToolsEdit)]
    public async Task<IActionResult> DeleteToolAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await toolService.DeleteAsync(id, cancellationToken));

    [HttpGet("credentials")]
    [Permission(PermissionCodes.FlowiseCredentialsView)]
    public async Task<IActionResult> GetCredentialsAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken)
        => ApiOk(await credentialService.GetPageAsync(query, cancellationToken));

    [HttpGet("credentials/{id}")]
    [Permission(PermissionCodes.FlowiseCredentialsView)]
    public async Task<IActionResult> GetCredentialAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await credentialService.GetAsync(id, cancellationToken));

    [HttpPost("credentials")]
    [Permission(PermissionCodes.FlowiseCredentialsEdit)]
    public async Task<IActionResult> CreateCredentialAsync([FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await credentialService.CreateAsync(request, cancellationToken));

    [HttpPut("credentials/{id}")]
    [Permission(PermissionCodes.FlowiseCredentialsEdit)]
    public async Task<IActionResult> UpdateCredentialAsync(string id, [FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await credentialService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("credentials/{id}")]
    [Permission(PermissionCodes.FlowiseCredentialsEdit)]
    public async Task<IActionResult> DeleteCredentialAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await credentialService.DeleteAsync(id, cancellationToken));

    [HttpPost("credentials/{id}/reveal")]
    [Permission(PermissionCodes.FlowiseRevealSecret)]
    public async Task<IActionResult> RevealCredentialAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await credentialService.RevealAsync(id, cancellationToken));

    [HttpGet("variables")]
    [Permission(PermissionCodes.FlowiseVariablesView)]
    public async Task<IActionResult> GetVariablesAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken)
        => ApiOk(await variableService.GetPageAsync(query, cancellationToken));

    [HttpGet("variables/{id}")]
    [Permission(PermissionCodes.FlowiseVariablesView)]
    public async Task<IActionResult> GetVariableAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await variableService.GetAsync(id, cancellationToken));

    [HttpPost("variables")]
    [Permission(PermissionCodes.FlowiseVariablesEdit)]
    public async Task<IActionResult> CreateVariableAsync([FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await variableService.CreateAsync(request, cancellationToken));

    [HttpPut("variables/{id}")]
    [Permission(PermissionCodes.FlowiseVariablesEdit)]
    public async Task<IActionResult> UpdateVariableAsync(string id, [FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await variableService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("variables/{id}")]
    [Permission(PermissionCodes.FlowiseVariablesEdit)]
    public async Task<IActionResult> DeleteVariableAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await variableService.DeleteAsync(id, cancellationToken));

    [HttpPost("variables/{id}/reveal")]
    [Permission(PermissionCodes.FlowiseRevealSecret)]
    public async Task<IActionResult> RevealVariableAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await variableService.RevealAsync(id, cancellationToken));

    [HttpGet("api-keys")]
    [Permission(PermissionCodes.FlowiseApiKeysView)]
    public async Task<IActionResult> GetApiKeysAsync([FromQuery] FlowiseStudioQuery query, CancellationToken cancellationToken)
        => ApiOk(await apiKeyService.GetPageAsync(query, cancellationToken));

    [HttpGet("api-keys/{id}")]
    [Permission(PermissionCodes.FlowiseApiKeysView)]
    public async Task<IActionResult> GetApiKeyAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await apiKeyService.GetAsync(id, cancellationToken));

    [HttpPost("api-keys")]
    [Permission(PermissionCodes.FlowiseApiKeysEdit)]
    public async Task<IActionResult> CreateApiKeyAsync([FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await apiKeyService.CreateAsync(request, cancellationToken));

    [HttpPut("api-keys/{id}")]
    [Permission(PermissionCodes.FlowiseApiKeysEdit)]
    public async Task<IActionResult> UpdateApiKeyAsync(string id, [FromBody] FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await apiKeyService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("api-keys/{id}")]
    [Permission(PermissionCodes.FlowiseApiKeysEdit)]
    public async Task<IActionResult> DeleteApiKeyAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await apiKeyService.DeleteAsync(id, cancellationToken));
}

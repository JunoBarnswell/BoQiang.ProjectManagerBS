using AsterERP.Api.Application.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/prompt-templates")]
public sealed class AiPromptTemplatesController(IAiPromptTemplateService service) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AiPromptView)]
    public async Task<IActionResult> GetPageAsync([FromQuery] GridQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetPageAsync(query, cancellationToken));
    }

    [HttpGet("options")]
    [Permission(PermissionCodes.AiPromptView)]
    public async Task<IActionResult> GetOptionsAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetOptionsAsync(cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.AiPromptAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] AiPromptTemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id}")]
    [Permission(PermissionCodes.AiPromptEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] AiPromptTemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.AiPromptDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("{id}/copy")]
    [Permission(PermissionCodes.AiPromptCopy)]
    public async Task<IActionResult> CopyAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await service.CopyAsync(id, cancellationToken));
    }

    [HttpPost("{id}/publish")]
    [Permission(PermissionCodes.AiPromptPublish)]
    public async Task<IActionResult> PublishAsync(string id, [FromBody] AiPromptPublishRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.PublishAsync(id, request, cancellationToken));
    }

    [HttpGet("{id}/versions")]
    [Permission(PermissionCodes.AiPromptView)]
    public async Task<IActionResult> GetVersionsAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetVersionsAsync(id, cancellationToken));
    }

    [HttpPost("test")]
    [Permission(PermissionCodes.AiPromptTest)]
    public async Task<IActionResult> TestAsync([FromBody] AiPromptTestRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.TestAsync(request, cancellationToken));
    }
}

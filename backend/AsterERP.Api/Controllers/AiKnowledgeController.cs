using AsterERP.Api.Application.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/knowledge")]
public sealed class AiKnowledgeController(AiKnowledgeService service) : BaseApiController
{
    [HttpGet("sources")]
    [Permission(PermissionCodes.AiKnowledgeView)]
    public async Task<IActionResult> GetSourcesAsync([FromQuery] GridQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetSourcesAsync(query, cancellationToken));
    }

    [HttpPost("sources")]
    [Permission(PermissionCodes.AiKnowledgeManage)]
    public async Task<IActionResult> CreateSourceAsync([FromBody] AiKnowledgeSourceUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.CreateSourceAsync(request, cancellationToken));
    }

    [HttpGet("documents")]
    [Permission(PermissionCodes.AiKnowledgeView)]
    public async Task<IActionResult> GetDocumentsAsync([FromQuery] string? sourceId, [FromQuery] GridQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await service.GetDocumentsAsync(sourceId, query, cancellationToken));
    }

    [HttpPost("reindex")]
    [Permission(PermissionCodes.AiKnowledgeManage)]
    public async Task<IActionResult> ReindexAsync([FromQuery] string? sourceId, CancellationToken cancellationToken)
    {
        return ApiOk(await service.ReindexAsync(sourceId, cancellationToken));
    }

    [HttpPost("search")]
    [Permission(PermissionCodes.AiKnowledgeView)]
    public async Task<IActionResult> SearchAsync([FromBody] AiKnowledgeSearchRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await service.SearchAsync(request, cancellationToken));
    }
}

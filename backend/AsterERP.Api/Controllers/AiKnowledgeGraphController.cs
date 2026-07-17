using AsterERP.Api.Application.Ai.KnowledgeGraph;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/ai/knowledge/graph")]
public sealed class AiKnowledgeGraphController(
    IAiKnowledgeGraphService graphService,
    IAiKnowledgeGraphBuildService buildService,
    IAiKnowledgeGraphImportExportService importExportService) : BaseApiController
{
    [HttpGet("overview")]
    [Permission(PermissionCodes.AiKnowledgeGraphView)]
    public async Task<IActionResult> GetOverviewAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await graphService.GetOverviewAsync(cancellationToken));
    }

    [HttpGet("node-types")]
    [Permission(PermissionCodes.AiKnowledgeGraphView)]
    public async Task<IActionResult> GetNodeTypesAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await graphService.GetNodeTypesAsync(cancellationToken));
    }

    [HttpGet("relation-types")]
    [Permission(PermissionCodes.AiKnowledgeGraphView)]
    public async Task<IActionResult> GetRelationTypesAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await graphService.GetRelationTypesAsync(cancellationToken));
    }

    [HttpPost("query")]
    [Permission(PermissionCodes.AiKnowledgeGraphSearch)]
    public async Task<IActionResult> QueryAsync([FromBody] AiKnowledgeGraphQueryRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await graphService.QueryAsync(request, cancellationToken));
    }

    [HttpPost("neighborhood")]
    [Permission(PermissionCodes.AiKnowledgeGraphSearch)]
    public async Task<IActionResult> GetNeighborhoodAsync([FromBody] AiKnowledgeGraphNeighborhoodRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await graphService.GetNeighborhoodAsync(request, cancellationToken));
    }

    [HttpPost("paths")]
    [Permission(PermissionCodes.AiKnowledgeGraphSearch)]
    public async Task<IActionResult> FindPathsAsync([FromBody] AiKnowledgeGraphPathRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await graphService.FindPathsAsync(request, cancellationToken));
    }

    [HttpPost("impact")]
    [Permission(PermissionCodes.AiKnowledgeGraphSearch)]
    public async Task<IActionResult> AnalyzeImpactAsync([FromBody] AiKnowledgeGraphImpactRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await graphService.AnalyzeImpactAsync(request, cancellationToken));
    }

    [HttpGet("nodes/{id}")]
    [Permission(PermissionCodes.AiKnowledgeGraphView)]
    public async Task<IActionResult> GetNodeAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await graphService.GetNodeAsync(id, cancellationToken));
    }

    [HttpPost("nodes")]
    [Permission(PermissionCodes.AiKnowledgeGraphEdit)]
    public async Task<IActionResult> CreateNodeAsync([FromBody] AiKnowledgeGraphNodeUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await graphService.CreateNodeAsync(request, cancellationToken));
    }

    [HttpPut("nodes/{id}")]
    [Permission(PermissionCodes.AiKnowledgeGraphEdit)]
    public async Task<IActionResult> UpdateNodeAsync(string id, [FromBody] AiKnowledgeGraphNodeUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await graphService.UpdateNodeAsync(id, request, cancellationToken));
    }

    [HttpDelete("nodes/{id}")]
    [Permission(PermissionCodes.AiKnowledgeGraphEdit)]
    public async Task<IActionResult> DeleteNodeAsync(string id, [FromQuery] bool cascade, CancellationToken cancellationToken)
    {
        return ApiOk(await graphService.DeleteNodeAsync(id, cascade, cancellationToken));
    }

    [HttpGet("edges/{id}")]
    [Permission(PermissionCodes.AiKnowledgeGraphView)]
    public async Task<IActionResult> GetEdgeAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await graphService.GetEdgeAsync(id, cancellationToken));
    }

    [HttpPost("edges")]
    [Permission(PermissionCodes.AiKnowledgeGraphEdit)]
    public async Task<IActionResult> CreateEdgeAsync([FromBody] AiKnowledgeGraphEdgeUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await graphService.CreateEdgeAsync(request, cancellationToken));
    }

    [HttpPut("edges/{id}")]
    [Permission(PermissionCodes.AiKnowledgeGraphEdit)]
    public async Task<IActionResult> UpdateEdgeAsync(string id, [FromBody] AiKnowledgeGraphEdgeUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await graphService.UpdateEdgeAsync(id, request, cancellationToken));
    }

    [HttpDelete("edges/{id}")]
    [Permission(PermissionCodes.AiKnowledgeGraphEdit)]
    public async Task<IActionResult> DeleteEdgeAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await graphService.DeleteEdgeAsync(id, cancellationToken));
    }

    [HttpPost("reindex")]
    [Permission(PermissionCodes.AiKnowledgeGraphReindex)]
    public async Task<IActionResult> ReindexAsync([FromBody] AiKnowledgeGraphBuildRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await buildService.ReindexAsync(request, cancellationToken));
    }

    [HttpGet("jobs/{id}")]
    [Permission(PermissionCodes.AiKnowledgeGraphView)]
    public async Task<IActionResult> GetJobAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await buildService.GetJobAsync(id, cancellationToken));
    }

    [HttpPost("import")]
    [Permission(PermissionCodes.AiKnowledgeGraphImport)]
    public async Task<IActionResult> ImportAsync([FromBody] AiKnowledgeGraphImportRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await importExportService.ImportAsync(request, cancellationToken));
    }

    [HttpPost("export")]
    [Permission(PermissionCodes.AiKnowledgeGraphExport)]
    public async Task<IActionResult> ExportAsync([FromBody] AiKnowledgeGraphExportRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await importExportService.ExportAsync(request, cancellationToken));
    }
}

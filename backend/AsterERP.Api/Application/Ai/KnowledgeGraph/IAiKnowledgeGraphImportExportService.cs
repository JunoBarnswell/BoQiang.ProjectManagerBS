using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Application.Ai.KnowledgeGraph;

public interface IAiKnowledgeGraphImportExportService
{
    Task<AiKnowledgeGraphImportResultDto> ImportAsync(AiKnowledgeGraphImportRequest request, CancellationToken cancellationToken);

    Task<AiKnowledgeGraphExportDto> ExportAsync(AiKnowledgeGraphExportRequest request, CancellationToken cancellationToken);
}

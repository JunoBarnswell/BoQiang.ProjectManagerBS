using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.KnowledgeGraph;

public sealed partial class AiKnowledgeGraphBuildService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    IAiKnowledgeGraphImportExportService importExportService) : IAiKnowledgeGraphBuildService
{
    private const int MaxTermsPerChunk = 12;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AiKnowledgeGraphBuildJobDto> ReindexAsync(AiKnowledgeGraphBuildRequest request, CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        var requestHash = BuildRequestHash(request);
        var existing = await db.Queryable<AiKnowledgeGraphBuildJobEntity>()
            .FirstAsync(item => !item.IsDeleted && item.SourceId == request.SourceId && item.RequestHash == requestHash, cancellationToken);
        if (existing is not null)
        {
            return AiKnowledgeGraphMapper.MapJob(existing);
        }

        var job = new AiKnowledgeGraphBuildJobEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            SourceId = string.IsNullOrWhiteSpace(request.SourceId) ? null : request.SourceId.Trim(),
            RequestHash = requestHash,
            Status = "Running",
            Progress = 1,
            StartedAt = DateTime.UtcNow
        };
        await db.Insertable(job).ExecuteCommandAsync(cancellationToken);

        try
        {
            var result = await BuildGraphAsync(job, request, workspace, cancellationToken);
            job.Status = "Completed";
            job.Progress = 100;
            job.CreatedCount = result.CreatedCount;
            job.UpdatedCount = result.UpdatedCount;
            job.SkippedCount = result.SkippedCount;
            job.FinishedAt = DateTime.UtcNow;
            job.UpdatedTime = job.FinishedAt;
            await db.Updateable(job).ExecuteCommandAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            job.Status = "Failed";
            job.Progress = 100;
            job.ErrorCode = exception is BusinessException businessException ? businessException.Code.ToString() : ErrorCodes.InternalError.ToString();
            job.ErrorMessage = exception.Message;
            job.FinishedAt = DateTime.UtcNow;
            job.UpdatedTime = job.FinishedAt;
            await db.Updateable(job).ExecuteCommandAsync(cancellationToken);
        }

        return AiKnowledgeGraphMapper.MapJob(job);
    }

    public async Task<AiKnowledgeGraphBuildJobDto> GetJobAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await db.Queryable<AiKnowledgeGraphBuildJobEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        return entity is null
            ? throw new ValidationException("图谱构建任务不存在", ErrorCodes.ParameterInvalid)
            : AiKnowledgeGraphMapper.MapJob(entity);
    }

    private async Task<AiKnowledgeGraphImportResultDto> BuildGraphAsync(
        AiKnowledgeGraphBuildJobEntity job,
        AiKnowledgeGraphBuildRequest request,
        AiWorkspace workspace,
        CancellationToken cancellationToken)
    {
        var sourceQuery = db.Queryable<AiKnowledgeSourceEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(request.SourceId))
        {
            var sourceId = request.SourceId.Trim();
            sourceQuery = sourceQuery.Where(item => item.Id == sourceId);
        }

        var sources = await sourceQuery.ToListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.SourceId) && sources.Count == 0)
        {
            throw new ValidationException("知识来源不存在", ErrorCodes.ParameterInvalid);
        }

        var documentIds = request.DocumentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var documentQuery = db.Queryable<AiKnowledgeDocumentEntity>().Where(item => !item.IsDeleted);
        var sourceIds = sources.Select(item => item.Id).ToList();
        if (sourceIds.Count > 0)
        {
            documentQuery = documentQuery.Where(item => sourceIds.Contains(item.SourceId));
        }

        if (documentIds.Count > 0)
        {
            documentQuery = documentQuery.Where(item => documentIds.Contains(item.Id));
        }

        var documents = await documentQuery.ToListAsync(cancellationToken);
        var documentIdsForChunkQuery = documents.Select(item => item.Id).ToList();
        var chunks = documents.Count == 0
            ? []
            : await db.Queryable<AiKnowledgeChunkEntity>()
                .Where(item => !item.IsDeleted && documentIdsForChunkQuery.Contains(item.DocumentId))
                .OrderBy(item => item.DocumentId)
                .OrderBy(item => item.ChunkIndex)
                .ToListAsync(cancellationToken);

        var import = BuildImportRequest(sources, documents, chunks, request.Mode);
        var result = await importExportService.ImportAsync(import, cancellationToken);
        job.Progress = 90;
        job.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(job).ExecuteCommandAsync(cancellationToken);
        return result;
    }

    private static AiKnowledgeGraphImportRequest BuildImportRequest(
        IReadOnlyList<AiKnowledgeSourceEntity> sources,
        IReadOnlyList<AiKnowledgeDocumentEntity> documents,
        IReadOnlyList<AiKnowledgeChunkEntity> chunks,
        string? mode)
    {
        var nodes = new List<AiKnowledgeGraphNodeUpsertRequest>();
        var edges = new List<AiKnowledgeGraphEdgeImportRequest>();

        foreach (var source in sources)
        {
            nodes.Add(new AiKnowledgeGraphNodeUpsertRequest
            {
                NodeKey = BuildSourceNodeKey(source.Id),
                NodeType = "source",
                DisplayName = source.SourceName,
                Summary = source.Description,
                SourceId = source.Id,
                MetadataJson = JsonSerializer.Serialize(new { source.SourceCode, source.SourceType }, JsonOptions)
            });
        }

        foreach (var document in documents)
        {
            nodes.Add(new AiKnowledgeGraphNodeUpsertRequest
            {
                NodeKey = BuildDocumentNodeKey(document.Id),
                NodeType = "document",
                DisplayName = document.DocumentName,
                SourceId = document.SourceId,
                DocumentId = document.Id,
                MetadataJson = JsonSerializer.Serialize(new { document.ContentType, document.IndexStatus, document.ChunkCount }, JsonOptions)
            });
            edges.Add(new AiKnowledgeGraphEdgeImportRequest
            {
                FromNodeKey = BuildSourceNodeKey(document.SourceId),
                ToNodeKey = BuildDocumentNodeKey(document.Id),
                RelationType = "contains",
                SourceId = document.SourceId,
                Weight = 1,
                EvidenceText = document.DocumentName
            });
        }

        foreach (var chunk in chunks)
        {
            nodes.Add(new AiKnowledgeGraphNodeUpsertRequest
            {
                NodeKey = BuildChunkNodeKey(chunk.Id),
                NodeType = "chunk",
                DisplayName = $"Chunk {chunk.ChunkIndex}",
                Summary = TrimContent(chunk.Content, 160),
                SourceId = chunk.SourceId,
                DocumentId = chunk.DocumentId,
                MetadataJson = chunk.MetadataJson
            });
            edges.Add(new AiKnowledgeGraphEdgeImportRequest
            {
                FromNodeKey = BuildDocumentNodeKey(chunk.DocumentId),
                ToNodeKey = BuildChunkNodeKey(chunk.Id),
                RelationType = "contains",
                SourceId = chunk.SourceId,
                Weight = 1,
                EvidenceText = TrimContent(chunk.Content, 200)
            });

            foreach (var term in ExtractTerms(chunk.Content).Take(MaxTermsPerChunk))
            {
                nodes.Add(new AiKnowledgeGraphNodeUpsertRequest
                {
                    NodeKey = BuildTermNodeKey(term),
                    NodeType = "term",
                    DisplayName = term,
                    SourceId = chunk.SourceId,
                    DocumentId = chunk.DocumentId
                });
                edges.Add(new AiKnowledgeGraphEdgeImportRequest
                {
                    FromNodeKey = BuildChunkNodeKey(chunk.Id),
                    ToNodeKey = BuildTermNodeKey(term),
                    RelationType = "mentions",
                    SourceId = chunk.SourceId,
                    Weight = 0.6m,
                    EvidenceText = TrimContent(chunk.Content, 200)
                });
            }
        }

        return new AiKnowledgeGraphImportRequest
        {
            Mode = string.IsNullOrWhiteSpace(mode) ? "Upsert" : mode.Trim(),
            RequestId = Guid.NewGuid().ToString("N"),
            Nodes = nodes
                .GroupBy(item => item.NodeKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList(),
            Edges = edges
                .GroupBy(item => $"{item.FromNodeKey}->{item.ToNodeKey}:{item.RelationType}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList()
        };
    }

    private static string BuildRequestHash(AiKnowledgeGraphBuildRequest request)
    {
        var payload = JsonSerializer.Serialize(new
        {
            sourceId = request.SourceId?.Trim(),
            documentIds = request.DocumentIds.Order(StringComparer.OrdinalIgnoreCase),
            mode = string.IsNullOrWhiteSpace(request.Mode) ? "Upsert" : request.Mode.Trim(),
            requestId = request.RequestId?.Trim()
        }, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static string BuildSourceNodeKey(string sourceId) => $"source:{sourceId}";

    private static string BuildDocumentNodeKey(string documentId) => $"document:{documentId}";

    private static string BuildChunkNodeKey(string chunkId) => $"chunk:{chunkId}";

    private static string BuildTermNodeKey(string term) => $"term:{term.ToLowerInvariant()}";

    private static string TrimContent(string content, int maxLength) =>
        content.Length <= maxLength ? content : content[..maxLength];

    private static IEnumerable<string> ExtractTerms(string content)
    {
        var matches = TermRegex().Matches(content);
        return matches
            .Select(match => match.Value.Trim().ToLowerInvariant())
            .Where(term => term.Length is >= 2 and <= 32)
            .GroupBy(term => term, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key);
    }

    [GeneratedRegex(@"[\p{L}\p{N}_-]{2,32}", RegexOptions.Compiled)]
    private static partial Regex TermRegex();
}

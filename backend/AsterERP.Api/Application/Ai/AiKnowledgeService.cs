using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai;

public sealed class AiKnowledgeService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext)
{
    private const string VectorStoreUnavailableReason =
        "官方 Microsoft/Semantic Kernel SQLite Vec VectorData provider 当前不可用，未启用自研向量检索替代。";

    public async Task<GridPageResult<AiKnowledgeSourceDto>> GetSourcesAsync(GridQuery query, CancellationToken cancellationToken)
    {
        var dbQuery = db.Queryable<AiKnowledgeSourceEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.SourceCode.Contains(keyword) || item.SourceName.Contains(keyword));
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 100), total);
        return new GridPageResult<AiKnowledgeSourceDto> { Total = total.Value, Items = rows.Select(MapSource).ToList() };
    }

    public async Task<AiKnowledgeSourceDto> CreateSourceAsync(AiKnowledgeSourceUpsertRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SourceCode) || string.IsNullOrWhiteSpace(request.SourceName))
        {
            throw new ValidationException("知识库来源编码和名称不能为空", ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        var code = request.SourceCode.Trim();
        var exists = await db.Queryable<AiKnowledgeSourceEntity>()
            .AnyAsync(item => !item.IsDeleted && item.SourceCode == code, cancellationToken);
        if (exists)
        {
            throw new ValidationException("知识库来源编码已存在", ErrorCodes.ParameterInvalid);
        }

        var entity = new AiKnowledgeSourceEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            SourceCode = code,
            SourceName = request.SourceName.Trim(),
            SourceType = string.IsNullOrWhiteSpace(request.SourceType) ? "Document" : request.SourceType.Trim(),
            Status = "FrameworkUnavailable",
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return MapSource(entity);
    }

    public async Task<GridPageResult<AiKnowledgeDocumentDto>> GetDocumentsAsync(string? sourceId, GridQuery query, CancellationToken cancellationToken)
    {
        var dbQuery = db.Queryable<AiKnowledgeDocumentEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(sourceId))
        {
            var targetSourceId = sourceId.Trim();
            dbQuery = dbQuery.Where(item => item.SourceId == targetSourceId);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.DocumentName.Contains(keyword));
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 100), total);
        return new GridPageResult<AiKnowledgeDocumentDto> { Total = total.Value, Items = rows.Select(MapDocument).ToList() };
    }

    public Task<bool> ReindexAsync(string? sourceId, CancellationToken cancellationToken)
    {
        throw new BusinessException(ErrorCodes.AiVectorStoreUnavailable, VectorStoreUnavailableReason);
    }

    public Task<AiKnowledgeSearchResponse> SearchAsync(AiKnowledgeSearchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new ValidationException("检索关键词不能为空", ErrorCodes.ParameterInvalid);
        }

        if (request.TopK is < 1 or > 20)
        {
            throw new ValidationException("topK 必须在 1 到 20 之间", ErrorCodes.ParameterInvalid);
        }

        throw new BusinessException(ErrorCodes.AiVectorStoreUnavailable, VectorStoreUnavailableReason);
    }

    private static AiKnowledgeSourceDto MapSource(AiKnowledgeSourceEntity entity) =>
        new()
        {
            Id = entity.Id,
            SourceCode = entity.SourceCode,
            SourceName = entity.SourceName,
            SourceType = entity.SourceType,
            Status = entity.Status,
            CreatedTime = entity.CreatedTime
        };

    private static AiKnowledgeDocumentDto MapDocument(AiKnowledgeDocumentEntity entity) =>
        new()
        {
            Id = entity.Id,
            SourceId = entity.SourceId,
            DocumentName = entity.DocumentName,
            ContentType = entity.ContentType,
            IndexStatus = entity.IndexStatus,
            ChunkCount = entity.ChunkCount,
            CreatedTime = entity.CreatedTime
        };
}

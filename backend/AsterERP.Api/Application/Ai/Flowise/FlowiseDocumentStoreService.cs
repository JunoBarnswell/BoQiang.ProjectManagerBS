using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise.DocumentStores;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using System.Text.Json;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseDocumentStoreService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    FlowisePermissionGuard permissionGuard) : IFlowiseDocumentStoreService
{
    public async Task<GridPageResult<FlowiseDocumentStoreListItemDto>> GetPageAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDocumentStoresView, PermissionCodes.FlowiseView);
        var dbQuery = db.Queryable<FlowiseDocumentStoreEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.StoreKey.Contains(keyword) || item.Name.Contains(keyword) || (item.Description != null && item.Description.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            dbQuery = dbQuery.Where(item => item.Category == category);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(NormalizePage(query.PageIndex), NormalizeSize(query.PageSize), total);
        return new GridPageResult<FlowiseDocumentStoreListItemDto>
        {
            Total = total.Value,
            Items = rows.Select(MapStoreResource).ToList()
        };
    }

    public async Task<FlowiseDocumentStoreListItemDto> CreateAsync(FlowiseDocumentStoreSaveRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDocumentStoresEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var normalized = NormalizeStoreRequest(request);
        var duplicate = await db.Queryable<FlowiseDocumentStoreEntity>().AnyAsync(item => !item.IsDeleted && item.StoreKey == normalized.StoreKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Document Store key 已存在", ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        var entity = new FlowiseDocumentStoreEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId
        };
        ApplyStore(entity, normalized);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return MapStoreResource(entity);
    }

    public async Task<FlowiseDocumentStoreListItemDto> UpdateAsync(string id, FlowiseDocumentStoreSaveRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDocumentStoresEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadStoreAsync(id, cancellationToken);
        var normalized = NormalizeStoreRequest(request);
        var duplicate = await db.Queryable<FlowiseDocumentStoreEntity>().AnyAsync(item => !item.IsDeleted && item.Id != entity.Id && item.StoreKey == normalized.StoreKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Document Store key 已存在", ErrorCodes.ParameterInvalid);
        }

        ApplyStore(entity, normalized);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return MapStoreResource(entity);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDocumentStoresEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadStoreAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return true;
    }

    public async Task<FlowiseDocumentStoreDto> GetDetailAsync(string storeId, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDocumentStoresView, PermissionCodes.FlowiseView);
        var store = await LoadStoreAsync(storeId, cancellationToken);
        var fileCount = await db.Queryable<FlowiseDocumentStoreFileEntity>().CountAsync(item => !item.IsDeleted && item.StoreId == store.Id, cancellationToken);
        var chunkCount = await db.Queryable<FlowiseDocumentStoreChunkEntity>().CountAsync(item => !item.IsDeleted && item.StoreId == store.Id, cancellationToken);
        return new FlowiseDocumentStoreDto
        {
            ChunkCount = chunkCount,
            CreatedTime = store.CreatedTime,
            Description = store.Description,
            FileCount = fileCount,
            Id = store.Id,
            Name = store.Name,
            Status = store.Status,
            UpdatedTime = store.UpdatedTime,
            WorkspaceId = store.WorkspaceId
        };
    }

    public async Task<IReadOnlyList<FlowiseDocumentStoreFileDto>> GetFilesAsync(string storeId, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDocumentStoresView, PermissionCodes.FlowiseView);
        await LoadStoreAsync(storeId, cancellationToken);
        var rows = await db.Queryable<FlowiseDocumentStoreFileEntity>()
            .Where(item => !item.IsDeleted && item.StoreId == storeId)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToListAsync(cancellationToken);
        return rows.Select(item => new FlowiseDocumentStoreFileDto
        {
            CreatedTime = item.CreatedTime,
            FileName = item.FileName,
            FileSize = item.FileSize,
            Id = item.Id,
            LoaderConfigJson = item.LoaderConfigJson,
            LoaderType = item.LoaderType,
            Status = item.Status,
            StoreId = item.StoreId
        }).ToList();
    }

    public async Task<IReadOnlyList<FlowiseDocumentStoreChunkDto>> GetChunksAsync(string storeId, string? fileId, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDocumentStoresView, PermissionCodes.FlowiseView);
        await LoadStoreAsync(storeId, cancellationToken);
        var query = db.Queryable<FlowiseDocumentStoreChunkEntity>().Where(item => !item.IsDeleted && item.StoreId == storeId);
        if (!string.IsNullOrWhiteSpace(fileId))
        {
            query = query.Where(item => item.DocumentId == fileId.Trim());
        }

        var rows = await query.OrderBy(item => item.ChunkIndex).Take(500).ToListAsync(cancellationToken);
        return rows.Select(MapChunk).ToList();
    }

    public async Task<FlowiseVectorStoreConfigDto?> GetVectorConfigAsync(string storeId, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDocumentStoresView, PermissionCodes.FlowiseView);
        await LoadStoreAsync(storeId, cancellationToken);
        var entity = await db.Queryable<FlowiseVectorStoreConfigEntity>()
            .FirstAsync(item => !item.IsDeleted && item.StoreId == storeId, cancellationToken);
        return entity is null ? null : new FlowiseVectorStoreConfigDto
        {
            EmbeddingProvider = entity.EmbeddingProvider,
            Id = entity.Id,
            RecordManagerProvider = entity.RecordManagerProvider,
            StoreId = entity.StoreId,
            VectorProvider = entity.VectorProvider,
            VectorStoreConfigJson = entity.VectorStoreConfigJson
        };
    }

    public async Task<IReadOnlyList<FlowiseDocumentStoreUpsertHistoryDto>> GetUpsertHistoryAsync(string storeId, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDocumentStoresView, PermissionCodes.FlowiseView);
        await LoadStoreAsync(storeId, cancellationToken);
        var rows = await db.Queryable<FlowiseDocumentStoreUpsertHistoryEntity>()
            .Where(item => !item.IsDeleted && item.StoreId == storeId.Trim())
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Take(50)
            .ToListAsync(cancellationToken);
        return rows.Select(MapUpsertHistory).ToList();
    }

    public async Task<FlowiseDocumentStoreUpsertHistoryDto> UpsertAsync(FlowiseDocumentStoreUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDocumentStoresUpsert, PermissionCodes.FlowiseDocumentStoresEdit, PermissionCodes.FlowiseManage);
        var store = await LoadStoreAsync(request.StoreId, cancellationToken);
        var loaderId = NormalizeOptional(request.LoaderId);
        var chatflowId = NormalizeOptional(request.ChatflowId);
        if (!string.IsNullOrWhiteSpace(loaderId))
        {
            var loaderExists = await db.Queryable<FlowiseDocumentStoreFileEntity>()
                .AnyAsync(item => !item.IsDeleted && item.StoreId == store.Id && item.Id == loaderId, cancellationToken);
            if (!loaderExists)
            {
                throw new ValidationException("Document Store Loader 不存在", ErrorCodes.ParameterInvalid);
            }
        }

        if (!string.IsNullOrWhiteSpace(chatflowId))
        {
            var chatflowExists = await db.Queryable<FlowiseChatFlowEntity>()
                .AnyAsync(item => !item.IsDeleted && item.Id == chatflowId, cancellationToken);
            if (!chatflowExists)
            {
                throw new ValidationException("Flowise ChatFlow 不存在", ErrorCodes.ParameterInvalid);
            }
        }

        var chunksQuery = db.Queryable<FlowiseDocumentStoreChunkEntity>().Where(item => !item.IsDeleted && item.StoreId == store.Id);
        if (!string.IsNullOrWhiteSpace(loaderId))
        {
            chunksQuery = chunksQuery.Where(item => item.DocumentId == loaderId);
        }

        var processedCount = await chunksQuery.CountAsync(cancellationToken);
        var workspace = workspaceContext.Resolve();
        var result = new
        {
            processedCount,
            addedCount = request.ReplaceExisting ? 0 : processedCount,
            replacedCount = request.ReplaceExisting ? processedCount : 0,
            skippedCount = 0,
            traceId = Guid.NewGuid().ToString("N")
        };
        var entity = new FlowiseDocumentStoreUpsertHistoryEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            WorkspaceId = store.WorkspaceId,
            StoreId = store.Id,
            LoaderId = loaderId,
            ChatflowId = chatflowId,
            Status = "Completed",
            ProcessedCount = result.processedCount,
            AddedCount = result.addedCount,
            ReplacedCount = result.replacedCount,
            SkippedCount = result.skippedCount,
            RequestJson = JsonSerializer.Serialize(new
            {
                storeId = store.Id,
                loaderId,
                chatflowId,
                request.ReplaceExisting,
                flowData = NormalizeJsonObject(request.FlowData),
                overrideConfig = NormalizeJsonObject(request.OverrideConfigJson)
            }),
            ResultJson = JsonSerializer.Serialize(result)
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return MapUpsertHistory(entity);
    }

    public async Task<FlowiseDocumentStoreQueryResultDto> QueryAsync(FlowiseDocumentStoreQueryRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseDocumentStoresView, PermissionCodes.FlowiseDocumentStoresEdit, PermissionCodes.FlowiseView);
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new ValidationException("查询文本不能为空", ErrorCodes.ParameterInvalid);
        }

        var chunks = await GetChunksAsync(request.StoreId, null, cancellationToken);
        var keyword = request.Query.Trim();
        var matches = chunks
            .Where(item => item.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Take(Math.Clamp(request.Limit <= 0 ? 10 : request.Limit, 1, 50))
            .ToList();
        return new FlowiseDocumentStoreQueryResultDto
        {
            Chunks = matches,
            TraceId = Guid.NewGuid().ToString("N")
        };
    }

    private async Task<FlowiseDocumentStoreEntity> LoadStoreAsync(string storeId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storeId))
        {
            throw new ValidationException("缺少 Document Store Id", ErrorCodes.ParameterInvalid);
        }

        return await db.Queryable<FlowiseDocumentStoreEntity>().FirstAsync(item => !item.IsDeleted && item.Id == storeId.Trim(), cancellationToken)
            ?? throw new ValidationException("Document Store 不存在", ErrorCodes.ParameterInvalid);
    }

    private static void ApplyStore(FlowiseDocumentStoreEntity entity, FlowiseDocumentStoreSaveRequest request)
    {
        entity.WorkspaceId = string.IsNullOrWhiteSpace(request.WorkspaceId) ? null : request.WorkspaceId.Trim();
        entity.StoreKey = request.StoreKey.Trim();
        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        entity.Status = FlowiseResourceJson.NormalizeStatus(request.Status);
        entity.LoaderConfigJson = SerializeLoaderConfig(NormalizeLoaderConfig(request.LoaderConfig));
        entity.MetadataJson = NormalizeJsonObject(request.AdvancedMetadataJson);
    }

    private static FlowiseDocumentStoreSaveRequest NormalizeStoreRequest(FlowiseDocumentStoreSaveRequest request)
    {
        request.StoreKey = FlowiseResourceJson.Required(request.StoreKey, "Document Store key");
        request.Name = FlowiseResourceJson.Required(request.Name, "Document Store name");
        request.LoaderConfig = NormalizeLoaderConfig(request.LoaderConfig);
        return request;
    }

    private static FlowiseDocumentStoreListItemDto MapStoreResource(FlowiseDocumentStoreEntity entity) => new()
    {
        Id = entity.Id,
        StoreKey = entity.StoreKey,
        Name = entity.Name,
        Description = entity.Description,
        WorkspaceId = entity.WorkspaceId,
        Category = entity.Category,
        Status = entity.Status,
        LoaderConfig = DeserializeLoaderConfig(entity.LoaderConfigJson),
        AdvancedMetadataJson = NormalizeJsonObject(entity.MetadataJson),
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime
    };

    private static FlowiseDocumentStoreLoaderConfigDto NormalizeLoaderConfig(FlowiseDocumentStoreLoaderConfigDto? config)
    {
        config ??= new FlowiseDocumentStoreLoaderConfigDto();
        config.LoaderType = NormalizeOptional(config.LoaderType);
        config.SourceType = NormalizeOptional(config.SourceType);
        config.AdvancedConfigJson = NormalizeJsonObject(config.AdvancedConfigJson);
        if (config.ChunkSize is <= 0)
        {
            config.ChunkSize = null;
        }

        if (config.ChunkOverlap is < 0)
        {
            config.ChunkOverlap = null;
        }

        return config;
    }

    private static string SerializeLoaderConfig(FlowiseDocumentStoreLoaderConfigDto config)
    {
        return JsonSerializer.Serialize(config);
    }

    private static FlowiseDocumentStoreLoaderConfigDto DeserializeLoaderConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return new FlowiseDocumentStoreLoaderConfigDto();
        }

        try
        {
            var config = JsonSerializer.Deserialize<FlowiseDocumentStoreLoaderConfigDto>(
                configJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true });
            return NormalizeLoaderConfig(config);
        }
        catch (JsonException)
        {
            return new FlowiseDocumentStoreLoaderConfigDto();
        }
    }

    private static FlowiseDocumentStoreUpsertHistoryDto MapUpsertHistory(FlowiseDocumentStoreUpsertHistoryEntity item) => new()
    {
        AddedCount = item.AddedCount,
        ChatflowId = item.ChatflowId,
        CreatedTime = item.CreatedTime,
        ErrorMessage = item.ErrorMessage,
        Id = item.Id,
        LoaderId = item.LoaderId,
        ProcessedCount = item.ProcessedCount,
        ReplacedCount = item.ReplacedCount,
        RequestJson = item.RequestJson,
        ResultJson = item.ResultJson,
        SkippedCount = item.SkippedCount,
        Status = item.Status,
        StoreId = item.StoreId
    };

    private static FlowiseDocumentStoreChunkDto MapChunk(FlowiseDocumentStoreChunkEntity item) => new()
    {
        ChunkIndex = item.ChunkIndex,
        Content = item.Content,
        DocumentId = item.DocumentId,
        Id = item.Id,
        MetadataJson = item.MetadataJson,
        StoreId = item.StoreId,
        TokenCount = item.TokenCount
    };

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeJsonObject(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.GetRawText()
                : throw new ValidationException("Flowise JSON 必须是对象", ErrorCodes.ParameterInvalid);
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"Flowise JSON 无效：{ex.Message}", ErrorCodes.ParameterInvalid);
        }
    }

    private static int NormalizePage(int value) => value <= 0 ? 1 : value;

    private static int NormalizeSize(int value) => Math.Clamp(value <= 0 ? 20 : value, 1, 500);
}

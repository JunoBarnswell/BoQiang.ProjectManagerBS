using AsterERP.Api.Infrastructure.Ai;
using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SqlSugar;

namespace AsterERP.Api.Application.Ai;

public sealed class AiModelConfigurationService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    IAiSecretProtector secretProtector,
    AiKernelChatRuntime chatRuntime) : IAiModelConfigurationService
{
    public async Task<GridPageResult<AiProviderDto>> GetProvidersAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = db.Queryable<AiProviderEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.ProviderCode.Contains(keyword) || item.ProviderName.Contains(keyword) || item.ProtocolType.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var enabled = query.Status.Equals("Enabled", StringComparison.OrdinalIgnoreCase);
            dbQuery = dbQuery.Where(item => item.IsEnabled == enabled);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 200), total);
        return new GridPageResult<AiProviderDto> { Total = total.Value, Items = rows.Select(MapProvider).ToList() };
    }

    public async Task<IReadOnlyList<AiProviderDto>> GetProviderOptionsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Queryable<AiProviderEntity>()
            .Where(item => !item.IsDeleted && item.IsEnabled)
            .OrderBy(item => item.ProviderName)
            .ToListAsync(cancellationToken);
        return rows.Select(MapProvider).ToList();
    }

    public async Task<AiProviderDto> CreateProviderAsync(AiProviderUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        ValidateProviderRequest(request);
        var code = NormalizeCode(request.ProviderCode);
        var exists = await db.Queryable<AiProviderEntity>()
            .AnyAsync(item => !item.IsDeleted && item.ProviderCode == code, cancellationToken);
        if (exists)
        {
            throw new ValidationException("供应商编码已存在", ErrorCodes.ParameterInvalid);
        }

        var entity = new AiProviderEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ProviderCode = code,
            ProviderName = request.ProviderName.Trim(),
            ProtocolType = NormalizeProtocol(request.ProtocolType),
            BaseUrl = request.BaseUrl.Trim(),
            IsEnabled = request.IsEnabled,
            TimeoutSeconds = Math.Clamp(request.TimeoutSeconds, 5, 600),
            ExtraParametersJson = NormalizeOptional(request.ExtraParametersJson)
        };

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            entity.ApiKeyCipherText = secretProtector.Protect(request.ApiKey);
            entity.ApiKeyMask = secretProtector.Mask(request.ApiKey);
        }

        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return MapProvider(entity);
    }

    public async Task<AiProviderDto> UpdateProviderAsync(string id, AiProviderUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ValidateProviderRequest(request);
        var entity = await RequireProviderAsync(id, cancellationToken);
        var code = NormalizeCode(request.ProviderCode);
        var exists = await db.Queryable<AiProviderEntity>()
            .AnyAsync(item => !item.IsDeleted && item.Id != id && item.ProviderCode == code, cancellationToken);
        if (exists)
        {
            throw new ValidationException("供应商编码已存在", ErrorCodes.ParameterInvalid);
        }

        entity.ProviderCode = code;
        entity.ProviderName = request.ProviderName.Trim();
        entity.ProtocolType = NormalizeProtocol(request.ProtocolType);
        entity.BaseUrl = request.BaseUrl.Trim();
        entity.IsEnabled = request.IsEnabled;
        entity.TimeoutSeconds = Math.Clamp(request.TimeoutSeconds, 5, 600);
        entity.ExtraParametersJson = NormalizeOptional(request.ExtraParametersJson);
        entity.UpdatedTime = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.ApiKey) && request.ApiKey.Trim() != "******")
        {
            entity.ApiKeyCipherText = secretProtector.Protect(request.ApiKey);
            entity.ApiKeyMask = secretProtector.Mask(request.ApiKey);
        }

        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return MapProvider(entity);
    }

    public async Task DeleteProviderAsync(string id, CancellationToken cancellationToken = default)
    {
        var used = await db.Queryable<AiModelConfigEntity>()
            .AnyAsync(item => !item.IsDeleted && item.ProviderId == id, cancellationToken);
        if (used)
        {
            throw new ValidationException("供应商已被模型配置引用，不能删除", ErrorCodes.StateChangeNotAllowed);
        }

        var entity = await RequireProviderAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<bool> TestProviderAsync(string id, CancellationToken cancellationToken = default)
    {
        var provider = await RequireProviderAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(provider.ApiKeyCipherText))
        {
            throw new ValidationException("请先配置供应商 API Key", ErrorCodes.AiProviderMissing);
        }

        var model = await db.Queryable<AiModelConfigEntity>()
            .Where(item => !item.IsDeleted && item.IsEnabled && item.ProviderId == id)
            .OrderBy(item => item.SortOrder)
            .FirstAsync(cancellationToken);
        if (model is null)
        {
            throw new ValidationException("请先为供应商配置至少一个启用模型", ErrorCodes.AiModelNotFound);
        }

        var request = new AiKernelChatRequest
        {
            Endpoint = new AiModelEndpoint
            {
                ProviderId = provider.Id,
                ProviderCode = provider.ProviderCode,
                ProtocolType = provider.ProtocolType,
                BaseUrl = provider.BaseUrl,
                ApiKey = secretProtector.Unprotect(provider.ApiKeyCipherText),
                ModelConfigId = model.Id,
                ModelCode = model.ModelCode,
                MaxOutputTokens = 16,
                DefaultTemperature = model.DefaultTemperature,
                DefaultTopP = model.DefaultTopP,
                ReasoningEffort = model.ReasoningEffort,
                TimeoutSeconds = Math.Min(provider.TimeoutSeconds, 30)
            },
            Messages = [new ChatMessageContent(AuthorRole.User, "ping")]
        };
        _ = await chatRuntime.CompleteAsync(request, cancellationToken);
        return true;
    }

    public async Task<AiProviderDto> SetProviderStatusAsync(string id, bool enabled, CancellationToken cancellationToken = default)
    {
        var entity = await RequireProviderAsync(id, cancellationToken);
        entity.IsEnabled = enabled;
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return MapProvider(entity);
    }

    public async Task<GridPageResult<AiModelConfigDto>> GetModelsAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = db.Queryable<AiModelConfigEntity, AiProviderEntity>((model, provider) => model.ProviderId == provider.Id)
            .Where((model, provider) => !model.IsDeleted && !provider.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where((model, provider) => model.ModelCode.Contains(keyword) || model.DisplayName.Contains(keyword) || provider.ProviderName.Contains(keyword));
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery
            .OrderBy((model, provider) => model.SortOrder)
            .OrderBy((model, provider) => model.CreatedTime, OrderByType.Desc)
            .Select((model, provider) => new AiModelConfigDto
            {
                Id = model.Id,
                ProviderId = model.ProviderId,
                ProviderName = provider.ProviderName,
                ModelCode = model.ModelCode,
                DisplayName = model.DisplayName,
                MaxContextTokens = model.MaxContextTokens,
                MaxOutputTokens = model.MaxOutputTokens,
                DefaultTemperature = model.DefaultTemperature,
                DefaultTopP = model.DefaultTopP,
                ThinkingEnabledDefault = model.ThinkingEnabledDefault,
                ReasoningEffort = model.ReasoningEffort,
                ToolStreamEnabledDefault = model.ToolStreamEnabledDefault,
                MaxParallelRuns = model.MaxParallelRuns,
                IsEnabled = model.IsEnabled,
                SortOrder = model.SortOrder
            })
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 200), total);
        return new GridPageResult<AiModelConfigDto> { Total = total.Value, Items = rows };
    }

    public async Task<IReadOnlyList<AiModelConfigDto>> GetModelOptionsAsync(CancellationToken cancellationToken = default)
    {
        var page = await GetModelsAsync(new GridQuery { PageIndex = 1, PageSize = 200 }, cancellationToken);
        return page.Items.Where(item => item.IsEnabled).ToList();
    }

    public async Task<AiModelConfigDto> CreateModelAsync(AiModelConfigUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        await ValidateModelRequestAsync(request, null, cancellationToken);
        var entity = new AiModelConfigEntity();
        ApplyModel(entity, request, workspace);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return await GetModelByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<AiModelConfigDto> UpdateModelAsync(string id, AiModelConfigUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var entity = await RequireModelAsync(id, cancellationToken);
        await ValidateModelRequestAsync(request, id, cancellationToken);
        ApplyModel(entity, request, workspace);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return await GetModelByIdAsync(entity.Id, cancellationToken);
    }

    public async Task DeleteModelAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireModelAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<AiModelConfigDto> SetModelStatusAsync(string id, bool enabled, CancellationToken cancellationToken = default)
    {
        var entity = await RequireModelAsync(id, cancellationToken);
        entity.IsEnabled = enabled;
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return await GetModelByIdAsync(id, cancellationToken);
    }

    public async Task<AiModelConfigDto> CopyModelAsync(string id, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var source = await RequireModelAsync(id, cancellationToken);
        var copy = new AiModelConfigEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ProviderId = source.ProviderId,
            ModelCode = await BuildCopyCodeAsync(source.ModelCode, cancellationToken),
            DisplayName = $"{source.DisplayName} 副本",
            MaxContextTokens = source.MaxContextTokens,
            MaxOutputTokens = source.MaxOutputTokens,
            DefaultTemperature = source.DefaultTemperature,
            DefaultTopP = source.DefaultTopP,
            ThinkingEnabledDefault = source.ThinkingEnabledDefault,
            ReasoningEffort = source.ReasoningEffort,
            ToolStreamEnabledDefault = source.ToolStreamEnabledDefault,
            MaxParallelRuns = source.MaxParallelRuns,
            IsEnabled = false,
            SortOrder = source.SortOrder + 1
        };
        await db.Insertable(copy).ExecuteCommandAsync(cancellationToken);
        return await GetModelByIdAsync(copy.Id, cancellationToken);
    }

    private async Task<AiModelConfigDto> GetModelByIdAsync(string id, CancellationToken cancellationToken)
    {
        var page = await GetModelsAsync(new GridQuery { PageIndex = 1, PageSize = 1, Keyword = null }, cancellationToken);
        var model = page.Items.FirstOrDefault(item => item.Id == id);
        if (model is not null)
        {
            return model;
        }

        var rows = await db.Queryable<AiModelConfigEntity, AiProviderEntity>((model, provider) => model.ProviderId == provider.Id)
            .Where((model, provider) => model.Id == id && !model.IsDeleted)
            .Select((model, provider) => new AiModelConfigDto
            {
                Id = model.Id,
                ProviderId = model.ProviderId,
                ProviderName = provider.ProviderName,
                ModelCode = model.ModelCode,
                DisplayName = model.DisplayName,
                MaxContextTokens = model.MaxContextTokens,
                MaxOutputTokens = model.MaxOutputTokens,
                DefaultTemperature = model.DefaultTemperature,
                DefaultTopP = model.DefaultTopP,
                ThinkingEnabledDefault = model.ThinkingEnabledDefault,
                ReasoningEffort = model.ReasoningEffort,
                ToolStreamEnabledDefault = model.ToolStreamEnabledDefault,
                MaxParallelRuns = model.MaxParallelRuns,
                IsEnabled = model.IsEnabled,
                SortOrder = model.SortOrder
            })
            .ToListAsync(cancellationToken);
        return rows.FirstOrDefault() ?? throw new NotFoundException("模型配置不存在", ErrorCodes.AiModelNotFound);
    }

    private async Task<AiProviderEntity> RequireProviderAsync(string id, CancellationToken cancellationToken) =>
        await db.Queryable<AiProviderEntity>().FirstAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
        ?? throw new NotFoundException("模型供应商不存在", ErrorCodes.AiProviderNotFound);

    private async Task<AiModelConfigEntity> RequireModelAsync(string id, CancellationToken cancellationToken) =>
        await db.Queryable<AiModelConfigEntity>().FirstAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
        ?? throw new NotFoundException("模型配置不存在", ErrorCodes.AiModelNotFound);

    private async Task<string> BuildCopyCodeAsync(string sourceCode, CancellationToken cancellationToken)
    {
        for (var index = 1; index <= 100; index++)
        {
            var code = $"{sourceCode}-copy{index}";
            var exists = await db.Queryable<AiModelConfigEntity>().AnyAsync(item => !item.IsDeleted && item.ModelCode == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        throw new ValidationException("无法生成唯一模型副本编码", ErrorCodes.ParameterInvalid);
    }

    private async Task ValidateModelRequestAsync(AiModelConfigUpsertRequest request, string? existingId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderId) || string.IsNullOrWhiteSpace(request.ModelCode) || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ValidationException("请填写供应商、模型编码和显示名称", ErrorCodes.ParameterInvalid);
        }

        _ = await RequireProviderAsync(request.ProviderId, cancellationToken);
        var modelCode = request.ModelCode.Trim();
        var exists = await db.Queryable<AiModelConfigEntity>()
            .AnyAsync(item => !item.IsDeleted && item.ProviderId == request.ProviderId && item.ModelCode == modelCode && item.Id != existingId, cancellationToken);
        if (exists)
        {
            throw new ValidationException("同一供应商下模型编码已存在", ErrorCodes.ParameterInvalid);
        }
    }

    private static void ApplyModel(AiModelConfigEntity entity, AiModelConfigUpsertRequest request, AiWorkspace workspace)
    {
        entity.TenantId = workspace.TenantId;
        entity.AppCode = workspace.AppCode;
        entity.ProviderId = request.ProviderId;
        entity.ModelCode = request.ModelCode.Trim();
        entity.DisplayName = request.DisplayName.Trim();
        entity.MaxContextTokens = Math.Max(4096, request.MaxContextTokens);
        entity.MaxOutputTokens = Math.Max(256, request.MaxOutputTokens);
        entity.DefaultTemperature = request.DefaultTemperature;
        entity.DefaultTopP = request.DefaultTopP;
        entity.ThinkingEnabledDefault = request.ThinkingEnabledDefault;
        entity.ReasoningEffort = NormalizeOptional(request.ReasoningEffort);
        entity.ToolStreamEnabledDefault = request.ToolStreamEnabledDefault;
        entity.MaxParallelRuns = Math.Clamp(request.MaxParallelRuns, 1, 50);
        entity.IsEnabled = request.IsEnabled;
        entity.SortOrder = request.SortOrder;
    }

    private static AiProviderDto MapProvider(AiProviderEntity entity) => new()
    {
        Id = entity.Id,
        ProviderCode = entity.ProviderCode,
        ProviderName = entity.ProviderName,
        ProtocolType = entity.ProtocolType,
        BaseUrl = entity.BaseUrl,
        ApiKeyMask = entity.ApiKeyMask,
        IsEnabled = entity.IsEnabled,
        TimeoutSeconds = entity.TimeoutSeconds,
        ExtraParametersJson = entity.ExtraParametersJson,
        CreatedTime = entity.CreatedTime
    };

    private static void ValidateProviderRequest(AiProviderUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderCode) || string.IsNullOrWhiteSpace(request.ProviderName) || string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            throw new ValidationException("请填写供应商编码、名称和 BaseUrl", ErrorCodes.ParameterInvalid);
        }
    }

    private static string NormalizeCode(string value) => value.Trim().ToLowerInvariant();

    private static string NormalizeProtocol(string value) =>
        string.IsNullOrWhiteSpace(value) ? "OpenAiCompatible" : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

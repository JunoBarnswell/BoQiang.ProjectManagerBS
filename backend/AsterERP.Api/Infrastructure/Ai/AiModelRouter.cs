using AsterERP.Api.Modules.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using System.Security.Cryptography;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Ai;

public sealed class AiModelRouter(ISqlSugarClient db, IAiSecretProtector secretProtector) : IAiModelRouter
{
    private const int DeepSeekV4ContextTokens = 1_000_000;
    private const int DeepSeekV4OutputTokens = 384_000;

    public async Task<AiModelEndpoint> ResolveAsync(string? modelConfigId, CancellationToken cancellationToken)
    {
        var model = string.IsNullOrWhiteSpace(modelConfigId)
            ? await db.Queryable<AiModelConfigEntity>()
                .Where(item => !item.IsDeleted && item.IsEnabled)
                .OrderBy(item => item.SortOrder)
                .OrderBy(item => item.CreatedTime)
                .FirstAsync(cancellationToken)
            : await db.Queryable<AiModelConfigEntity>()
                .Where(item => item.Id == modelConfigId && !item.IsDeleted)
                .FirstAsync(cancellationToken);

        if (model is null)
        {
            throw new NotFoundException("模型配置不存在", ErrorCodes.AiModelNotFound);
        }

        if (!model.IsEnabled)
        {
            throw new ValidationException("模型配置已停用", ErrorCodes.AiModelDisabled);
        }

        var provider = await db.Queryable<AiProviderEntity>()
            .Where(item => item.Id == model.ProviderId && !item.IsDeleted)
            .FirstAsync(cancellationToken);

        if (provider is null)
        {
            throw new NotFoundException("模型供应商不存在", ErrorCodes.AiProviderNotFound);
        }

        if (!provider.IsEnabled)
        {
            throw new ValidationException("模型供应商已停用", ErrorCodes.AiProviderMissing);
        }

        if (string.IsNullOrWhiteSpace(provider.ApiKeyCipherText))
        {
            throw new ValidationException("模型供应商未配置 API Key", ErrorCodes.AiProviderMissing);
        }

        var apiKey = UnprotectApiKey(provider);
        return new AiModelEndpoint
        {
            ProviderId = provider.Id,
            ProviderCode = provider.ProviderCode,
            ProtocolType = provider.ProtocolType,
            BaseUrl = provider.BaseUrl,
            ApiKey = apiKey,
            ModelConfigId = model.Id,
            ModelCode = model.ModelCode,
            MaxContextTokens = ResolveMaxContextTokens(provider.ProtocolType, model.ModelCode, model.MaxContextTokens),
            MaxOutputTokens = ResolveMaxOutputTokens(provider.ProtocolType, model.ModelCode, model.MaxOutputTokens),
            DefaultTemperature = model.DefaultTemperature,
            DefaultTopP = model.DefaultTopP,
            ThinkingEnabledDefault = model.ThinkingEnabledDefault,
            ReasoningEffort = model.ReasoningEffort,
            ToolStreamEnabledDefault = model.ToolStreamEnabledDefault,
            TimeoutSeconds = provider.TimeoutSeconds
        };
    }

    private static int ResolveMaxContextTokens(string protocolType, string modelCode, int configuredTokens) =>
        IsDeepSeekV4(protocolType, modelCode) ? DeepSeekV4ContextTokens : Math.Max(4096, configuredTokens);

    private static int ResolveMaxOutputTokens(string protocolType, string modelCode, int configuredTokens) =>
        IsDeepSeekV4(protocolType, modelCode) ? DeepSeekV4OutputTokens : Math.Max(256, configuredTokens);

    private static bool IsDeepSeekV4(string protocolType, string modelCode) =>
        protocolType.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase) &&
        (modelCode.Equals("deepseek-v4-pro", StringComparison.OrdinalIgnoreCase) ||
         modelCode.Equals("deepseek-v4-flash", StringComparison.OrdinalIgnoreCase));

    private string UnprotectApiKey(AiProviderEntity provider)
    {
        try
        {
            return secretProtector.Unprotect(provider.ApiKeyCipherText!);
        }
        catch (CryptographicException ex)
        {
            throw new ValidationException($"模型供应商 {provider.ProviderName} 的 API Key 无法解密，请重新保存供应商 API Key。{ex.Message}", ErrorCodes.AiProviderMissing);
        }
    }
}

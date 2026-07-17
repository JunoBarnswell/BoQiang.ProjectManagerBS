namespace AsterERP.Contracts.Ai;

public sealed class AiProviderDto
{
    public string Id { get; set; } = string.Empty;

    public string ProviderCode { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public string ProtocolType { get; set; } = "OpenAiCompatible";

    public string BaseUrl { get; set; } = string.Empty;

    public string? ApiKeyMask { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 120;

    public string? ExtraParametersJson { get; set; }

    public DateTime CreatedTime { get; set; }
}

public sealed class AiProviderUpsertRequest
{
    public string ProviderCode { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public string ProtocolType { get; set; } = "OpenAiCompatible";

    public string BaseUrl { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 120;

    public string? ExtraParametersJson { get; set; }
}

public sealed class AiModelConfigDto
{
    public string Id { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public string ModelCode { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int MaxContextTokens { get; set; }

    public int MaxOutputTokens { get; set; }

    public decimal? DefaultTemperature { get; set; }

    public decimal? DefaultTopP { get; set; }

    public bool ThinkingEnabledDefault { get; set; }

    public string? ReasoningEffort { get; set; }

    public bool ToolStreamEnabledDefault { get; set; }

    public int MaxParallelRuns { get; set; } = 3;

    public bool IsEnabled { get; set; } = true;

    public int SortOrder { get; set; }
}

public sealed class AiModelConfigUpsertRequest
{
    public string ProviderId { get; set; } = string.Empty;

    public string ModelCode { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int MaxContextTokens { get; set; } = 64000;

    public int MaxOutputTokens { get; set; } = 8192;

    public decimal? DefaultTemperature { get; set; }

    public decimal? DefaultTopP { get; set; }

    public bool ThinkingEnabledDefault { get; set; } = true;

    public string? ReasoningEffort { get; set; }

    public bool ToolStreamEnabledDefault { get; set; }

    public int MaxParallelRuns { get; set; } = 3;

    public bool IsEnabled { get; set; } = true;

    public int SortOrder { get; set; }
}

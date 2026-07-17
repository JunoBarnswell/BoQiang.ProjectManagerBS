namespace AsterERP.Api.Infrastructure.Ai;

public sealed class AiModelEndpoint
{
    public string ProviderId { get; set; } = string.Empty;

    public string ProviderCode { get; set; } = string.Empty;

    public string ProtocolType { get; set; } = "OpenAiCompatible";

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ModelConfigId { get; set; } = string.Empty;

    public string ModelCode { get; set; } = string.Empty;

    public int MaxContextTokens { get; set; }

    public int MaxOutputTokens { get; set; }

    public decimal? DefaultTemperature { get; set; }

    public decimal? DefaultTopP { get; set; }

    public bool ThinkingEnabledDefault { get; set; }

    public string? ReasoningEffort { get; set; }

    public bool ToolStreamEnabledDefault { get; set; }

    public int TimeoutSeconds { get; set; } = 120;
}

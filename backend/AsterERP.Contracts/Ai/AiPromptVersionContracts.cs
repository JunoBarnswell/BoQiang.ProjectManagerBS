namespace AsterERP.Contracts.Ai;

public sealed class AiPromptVersionDto
{
    public string Id { get; set; } = string.Empty;

    public string PromptTemplateId { get; set; } = string.Empty;

    public int VersionNo { get; set; }

    public string SystemPrompt { get; set; } = string.Empty;

    public string? UserPromptTemplate { get; set; }

    public string? VariablesJson { get; set; }

    public string Status { get; set; } = "Draft";

    public DateTime CreatedTime { get; set; }
}

public sealed class AiPromptPublishRequest
{
    public string? ChangeSummary { get; set; }
}

public sealed class AiPromptTestRequest
{
    public string PromptTemplateId { get; set; } = string.Empty;

    public string Input { get; set; } = string.Empty;

    public Dictionary<string, string> Variables { get; set; } = [];
}

public sealed class AiPromptTestResponse
{
    public string RenderedSystemPrompt { get; set; } = string.Empty;

    public string RenderedUserPrompt { get; set; } = string.Empty;
}

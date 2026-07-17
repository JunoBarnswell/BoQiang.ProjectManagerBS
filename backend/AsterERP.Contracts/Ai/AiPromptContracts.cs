namespace AsterERP.Contracts.Ai;

public sealed class AiPromptTemplateDto
{
    public string Id { get; set; } = string.Empty;

    public string TemplateCode { get; set; } = string.Empty;

    public string TemplateName { get; set; } = string.Empty;

    public string Category { get; set; } = "general";

    public string SystemPrompt { get; set; } = string.Empty;

    public string? UserPromptTemplate { get; set; }

    public string? VariablesJson { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int SortOrder { get; set; }
}

public sealed class AiPromptTemplateUpsertRequest
{
    public string TemplateCode { get; set; } = string.Empty;

    public string TemplateName { get; set; } = string.Empty;

    public string Category { get; set; } = "general";

    public string SystemPrompt { get; set; } = string.Empty;

    public string? UserPromptTemplate { get; set; }

    public string? VariablesJson { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int SortOrder { get; set; }
}

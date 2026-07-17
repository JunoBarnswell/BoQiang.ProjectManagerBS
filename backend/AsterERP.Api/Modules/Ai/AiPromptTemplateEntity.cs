using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_prompt_templates")]
public sealed class AiPromptTemplateEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string TemplateCode { get; set; } = string.Empty;

    public string TemplateName { get; set; } = string.Empty;

    public string Category { get; set; } = "general";

    public string SystemPrompt { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? UserPromptTemplate { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? VariablesJson { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int SortOrder { get; set; }
}

using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_prompt_versions")]
public sealed class AiPromptVersionEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string PromptTemplateId { get; set; } = string.Empty;

    public int VersionNo { get; set; }

    public string SystemPrompt { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? UserPromptTemplate { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? VariablesJson { get; set; }

    public string Status { get; set; } = "Draft";

    [SugarColumn(IsNullable = true)]
    public string? ChangeSummary { get; set; }
}

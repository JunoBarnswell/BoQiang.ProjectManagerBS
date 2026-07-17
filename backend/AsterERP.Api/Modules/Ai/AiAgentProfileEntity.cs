using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_agent_profiles")]
public sealed class AiAgentProfileEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string AgentCode { get; set; } = string.Empty;

    public string AgentName { get; set; } = string.Empty;

    public string RolePrompt { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ModelConfigId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? PromptTemplateId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? AllowedFunctionsJson { get; set; }

    public bool IsCoordinator { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int SortOrder { get; set; }
}

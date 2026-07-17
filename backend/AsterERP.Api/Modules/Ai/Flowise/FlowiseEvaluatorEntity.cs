using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_evaluators")]
public sealed class FlowiseEvaluatorEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }
    public string EvaluatorKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? EvaluatorType { get; set; }
    public string Status { get; set; } = "Enabled";
    public string DefinitionJson { get; set; } = "{}";
    public string MetadataJson { get; set; } = "{}";
}

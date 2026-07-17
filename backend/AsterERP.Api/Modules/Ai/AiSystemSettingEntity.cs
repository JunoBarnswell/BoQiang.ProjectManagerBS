using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_system_settings")]
public sealed class AiSystemSettingEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string SettingKey { get; set; } = string.Empty;

    public string SettingValue { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ValueType { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Description { get; set; }
}

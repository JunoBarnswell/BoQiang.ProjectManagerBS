using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.CodeRules;

[SugarTable("system_code_rule_segments")]
public sealed class SystemCodeRuleSegmentEntity : EntityBase
{
    public string CodeRuleId { get; set; } = string.Empty;

    public string SegmentType { get; set; } = "Static";

    [SugarColumn(IsNullable = true)]
    public string? SegmentValue { get; set; }

    public int SegmentLength { get; set; }

    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; } = true;
}

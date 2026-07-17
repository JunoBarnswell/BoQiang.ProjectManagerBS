using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.CodeRules;

[SugarTable("system_code_rules")]
public sealed class SystemCodeRuleEntity : EntityBase
{
    public string RuleName { get; set; } = string.Empty;

    public string RuleCode { get; set; } = string.Empty;

    public string ResetPolicy { get; set; } = "Daily";

    [SugarColumn(IsNullable = true)]
    public string? CurrentDateKey { get; set; }

    public int CurrentSequence { get; set; }

    public bool IsEnabled { get; set; } = true;
}

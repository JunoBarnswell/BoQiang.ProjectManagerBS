namespace AsterERP.Contracts.CodeRules;

public sealed record CodeRulePreviewResponse(
    string RuleCode,
    string RuleName,
    string GeneratedCode);

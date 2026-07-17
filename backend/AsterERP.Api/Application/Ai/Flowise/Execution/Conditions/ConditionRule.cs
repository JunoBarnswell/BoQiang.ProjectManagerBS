namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed record ConditionRule(string Type, string Expression, string Operation, string Value1, string Value2);

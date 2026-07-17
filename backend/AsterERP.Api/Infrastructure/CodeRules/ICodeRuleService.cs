namespace AsterERP.Api.Infrastructure.CodeRules;

public interface ICodeRuleService
{
    Task<string> GenerateAsync(string ruleCode, CancellationToken cancellationToken = default);

    Task<string> PreviewAsync(string ruleCode, CancellationToken cancellationToken = default);
}

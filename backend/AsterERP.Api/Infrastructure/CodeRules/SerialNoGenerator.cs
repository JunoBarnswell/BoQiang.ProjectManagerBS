namespace AsterERP.Api.Infrastructure.CodeRules;

public sealed class SerialNoGenerator(ICodeRuleService codeRuleService)
{
    public Task<string> NextAsync(string ruleCode, CancellationToken cancellationToken = default)
    {
        return codeRuleService.GenerateAsync(ruleCode, cancellationToken);
    }

    public Task<string> PreviewAsync(string ruleCode, CancellationToken cancellationToken = default)
    {
        return codeRuleService.PreviewAsync(ruleCode, cancellationToken);
    }
}

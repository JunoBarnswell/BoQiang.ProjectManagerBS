using AsterERP.Api.Infrastructure.CodeRules;
using AsterERP.Api.Infrastructure.Dicts;
using AsterERP.Api.Infrastructure.Logging;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Modules.System.CodeRules;
using AsterERP.Contracts.CodeRules;
using AsterERP.Contracts.Logs;
using AsterERP.Shared;

namespace AsterERP.Api.Application.System.Foundation;

public sealed class SystemFoundationService(
    IDictService dictService,
    IRepository<SystemCodeRuleEntity> codeRuleRepository,
    ICodeRuleService codeRuleService,
    IOperationLogService operationLogService) : ISystemFoundationService
{
    public Task<IReadOnlyList<OptionItem>> GetDictOptionsAsync(
        string dictCode,
        CancellationToken cancellationToken = default) =>
        dictService.GetOptionsAsync(dictCode, cancellationToken);

    public async Task<CodeRulePreviewResponse> PreviewCodeAsync(
        string ruleCode,
        CancellationToken cancellationToken = default)
    {
        var rule = await codeRuleRepository.FirstOrDefaultAsync(
            item => item.RuleCode == ruleCode && item.IsEnabled,
            cancellationToken);
        var generatedCode = await codeRuleService.PreviewAsync(ruleCode, cancellationToken);
        return new CodeRulePreviewResponse(ruleCode, rule?.RuleName ?? ruleCode, generatedCode);
    }

    public async Task<CodeRulePreviewResponse> GenerateCodeAsync(
        string ruleCode,
        CancellationToken cancellationToken = default)
    {
        var generatedCode = await codeRuleService.GenerateAsync(ruleCode, cancellationToken);
        return new CodeRulePreviewResponse(ruleCode, ruleCode, generatedCode);
    }

    public async Task<IReadOnlyList<OperationLogResponse>> GetRecentOperationLogsAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        return await operationLogService.RecentAsync(take, cancellationToken);
    }

    public Task<GridPageResult<OperationLogResponse>> GetOperationLogsAsync(
        OperationLogQueryRequest request,
        CancellationToken cancellationToken = default) =>
        operationLogService.GetPageAsync(request, cancellationToken);

    public Task<OperationLogDetailResponse> GetOperationLogDetailAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        operationLogService.GetDetailAsync(id, cancellationToken);
}

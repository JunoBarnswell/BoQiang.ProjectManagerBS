using AsterERP.Contracts.CodeRules;
using AsterERP.Contracts.Logs;
using AsterERP.Shared;

namespace AsterERP.Api.Application.System.Foundation;

public interface ISystemFoundationService
{
    Task<IReadOnlyList<OptionItem>> GetDictOptionsAsync(string dictCode, CancellationToken cancellationToken = default);

    Task<CodeRulePreviewResponse> PreviewCodeAsync(string ruleCode, CancellationToken cancellationToken = default);

    Task<CodeRulePreviewResponse> GenerateCodeAsync(string ruleCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OperationLogResponse>> GetRecentOperationLogsAsync(int take = 20, CancellationToken cancellationToken = default);

    Task<GridPageResult<OperationLogResponse>> GetOperationLogsAsync(
        OperationLogQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationLogDetailResponse> GetOperationLogDetailAsync(string id, CancellationToken cancellationToken = default);
}

using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowiseChatflowService
{
    Task<GridPageResult<FlowiseChatflowDto>> GetPageAsync(FlowiseChatflowQuery query, CancellationToken cancellationToken);

    Task<FlowiseChatflowDto> GetAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseChatflowDto> CreateAsync(FlowiseChatflowUpsertRequest request, CancellationToken cancellationToken);

    Task<FlowiseChatflowDto> UpdateAsync(string id, FlowiseChatflowUpsertRequest request, CancellationToken cancellationToken);

    Task<FlowiseChatflowDto> UpdateConfigurationAsync(string id, FlowiseChatflowConfigurationRequest request, CancellationToken cancellationToken);

    Task<FlowiseChatflowDto> UpdateDomainsAsync(string id, FlowiseChatflowDomainsRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);

    Task<FlowiseCanvasValidationResultDto> ValidateFlowDataAsync(string flowData, CancellationToken cancellationToken);

    Task<FlowiseScheduleStatusDto> GetScheduleStatusAsync(string id, CancellationToken cancellationToken);

    Task<GridPageResult<FlowiseScheduleTriggerLogDto>> GetScheduleTriggerLogsAsync(string id, FlowiseScheduleLogQuery query, CancellationToken cancellationToken);
}

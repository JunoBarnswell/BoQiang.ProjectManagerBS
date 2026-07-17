using AsterERP.Api.Modules.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowiseExecutionTrackingService
{
    Task<FlowiseScheduleTriggerLogEntity?> CreateScheduleTriggerLogAsync(
        FlowiseChatFlowEntity chatflow,
        FlowiseExecutionEntity execution,
        string inputJson,
        CancellationToken cancellationToken);

    Task CompleteScheduleTriggerLogAsync(
        FlowiseScheduleTriggerLogEntity? log,
        FlowiseExecutionEntity execution,
        CancellationToken cancellationToken);

    Task WriteExecutionAuditAsync(
        FlowiseChatFlowEntity chatflow,
        FlowiseExecutionEntity execution,
        CancellationToken cancellationToken);

    Task WriteMcpExecutionAuditAsync(
        FlowiseChatFlowEntity chatflow,
        FlowiseExecutionEntity execution,
        CancellationToken cancellationToken);
}

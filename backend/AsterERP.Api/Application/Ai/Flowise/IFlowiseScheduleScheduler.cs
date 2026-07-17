using AsterERP.Api.Modules.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowiseScheduleScheduler
{
    Task ApplyAsync(FlowiseScheduleRecordEntity? record, CancellationToken cancellationToken = default);
}

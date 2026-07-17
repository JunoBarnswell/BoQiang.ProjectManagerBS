using AsterERP.Contracts.Workflows;

namespace AsterERP.Api.Application.Workflows;

public interface IWorkflowReportAppService
{
    Task<WorkflowReportOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default);
}

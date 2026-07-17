using AsterERP.Contracts.Workflows;

namespace AsterERP.Api.Application.Workflows;

public interface IWorkflowParticipantAppService
{
    Task<IReadOnlyList<WorkflowParticipantResponse>> QueryAsync(string? keyword, string? type, CancellationToken cancellationToken = default);
}

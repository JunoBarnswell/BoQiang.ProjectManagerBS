using AsterERP.Workflow.Approval.Api.Models.Hr;

namespace AsterERP.Workflow.Approval.Core.Services.Hr;

public interface ILeaveService
{
    Task SendMessageAsync(List<string> userCodes, CancellationToken cancellationToken = default);
    Task SaveLeaveAsync(Leave leave, CancellationToken cancellationToken = default);
    Task<Leave?> GetLeaveByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Leave?> GetLeaveByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default);
}

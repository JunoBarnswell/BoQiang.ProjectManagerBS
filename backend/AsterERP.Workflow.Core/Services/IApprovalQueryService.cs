using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Services;

public interface IApprovalQueryService
{
    Task<List<ApprovalTaskView>> GetPendingTasksForAssigneeAsync(string assignee, CancellationToken cancellationToken = default);
    Task<List<ApprovalTaskView>> GetCandidateTasksForUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<List<ApprovalTaskView>> GetCandidateTasksForGroupAsync(string groupId, CancellationToken cancellationToken = default);
    Task<List<ApprovalTaskView>> GetCompletedTasksForAssigneeAsync(string assignee, CancellationToken cancellationToken = default);
    Task<List<ApprovalProcessView>> GetStartedProcessesAsync(string initiator, CancellationToken cancellationToken = default);
    Task<List<ApprovalProcessView>> GetCompletedProcessesAsync(string? businessKey = null, CancellationToken cancellationToken = default);
    Task<ApprovalHistoryReport> GetApprovalHistoryAsync(string processInstanceId, CancellationToken cancellationToken = default);
}

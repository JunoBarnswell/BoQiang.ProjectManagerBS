namespace AsterERP.Workflow.Core.Security;

public interface IAccessControlService
{
    global::System.Threading.Tasks.Task<bool> CanStartProcessAsync(string userId, string processDefinitionId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<bool> CanClaimTaskAsync(string userId, string taskId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<bool> CanCompleteTaskAsync(string userId, string taskId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<bool> CanViewProcessInstanceAsync(string userId, string processInstanceId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<bool> CanDeleteProcessInstanceAsync(string userId, string processInstanceId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<bool> CanViewTaskAsync(string userId, string taskId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<bool> CanAdminAsync(string userId, CancellationToken cancellationToken = default);
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Cmd;

namespace AsterERP.Workflow.Core.Services;

public interface IHistoryService
{
    Task<List<HistoricProcessInstance>> GetHistoricProcessInstancesAsync(CancellationToken cancellationToken = default);
    Task<HistoricProcessInstance?> GetHistoricProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task<List<HistoricTaskInstance>> GetHistoricTaskInstancesAsync(CancellationToken cancellationToken = default);
    Task<List<HistoricActivityInstance>> GetHistoricActivityInstancesAsync(CancellationToken cancellationToken = default);
    Task<List<HistoricVariableInstance>> GetHistoricVariableInstancesAsync(CancellationToken cancellationToken = default);
    Task<List<HistoricDetail>> GetHistoricDetailsAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task<List<HistoricIdentityLink>> GetHistoricIdentityLinksAsync(string processInstanceId, CancellationToken cancellationToken = default);
    IHistoricProcessInstanceQuery CreateHistoricProcessInstanceQuery();
    IHistoricTaskInstanceQuery CreateHistoricTaskInstanceQuery();
    IHistoricActivityInstanceQuery CreateHistoricActivityInstanceQuery();
    IHistoricVariableInstanceQuery CreateHistoricVariableInstanceQuery();
    IHistoricDetailQuery CreateHistoricDetailQuery();
    IProcessInstanceHistoryLogQuery CreateProcessInstanceHistoryLogQuery(string processInstanceId);
    Task DeleteHistoricProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task DeleteHistoricTaskInstanceAsync(string taskId, CancellationToken cancellationToken = default);
    Task<List<IdentityLinkEntity>> GetHistoricIdentityLinksForProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task<List<IdentityLinkEntity>> GetHistoricIdentityLinksForTaskAsync(string taskId, CancellationToken cancellationToken = default);
}

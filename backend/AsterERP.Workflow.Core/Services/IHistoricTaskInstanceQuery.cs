using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Services;

public interface IHistoricTaskInstanceQuery
{
    IHistoricTaskInstanceQuery ProcessDefinitionId(string processDefinitionId);
    IHistoricTaskInstanceQuery TaskAssignee(string assignee);
    IHistoricTaskInstanceQuery ProcessInstanceId(string processInstanceId);
    IHistoricTaskInstanceQuery TaskDefinitionKey(string taskDefinitionKey);
    IHistoricTaskInstanceQuery Unfinished();
    IHistoricTaskInstanceQuery Finished();
    Task<List<HistoricTaskInstance>> ListAsync(CancellationToken cancellationToken = default);
    Task<long> CountAsync(CancellationToken cancellationToken = default);
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Services;

public interface IHistoricProcessInstanceQuery
{
    IHistoricProcessInstanceQuery ProcessDefinitionId(string processDefinitionId);
    IHistoricProcessInstanceQuery ProcessDefinitionKey(string processDefinitionKey);
    IHistoricProcessInstanceQuery ProcessInstanceId(string processInstanceId);
    IHistoricProcessInstanceQuery Unfinished();
    IHistoricProcessInstanceQuery Finished();
    Task<List<HistoricProcessInstance>> ListAsync(CancellationToken cancellationToken = default);
    Task<long> CountAsync(CancellationToken cancellationToken = default);
}

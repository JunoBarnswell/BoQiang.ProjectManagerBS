using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Services;

public interface IHistoricActivityInstanceQuery
{
    IHistoricActivityInstanceQuery ProcessInstanceId(string processInstanceId);
    IHistoricActivityInstanceQuery ActivityId(string activityId);
    IHistoricActivityInstanceQuery ActivityType(string activityType);
    IHistoricActivityInstanceQuery ProcessDefinitionId(string processDefinitionId);
    Task<List<HistoricActivityInstance>> ListAsync(CancellationToken cancellationToken = default);
    Task<long> CountAsync(CancellationToken cancellationToken = default);
}

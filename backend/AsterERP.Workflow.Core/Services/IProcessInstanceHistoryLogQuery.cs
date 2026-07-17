using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Services;

public interface IProcessInstanceHistoryLogQuery
{
    IProcessInstanceHistoryLogQuery ProcessInstanceId(string processInstanceId);
    IProcessInstanceHistoryLogQuery IncludeActivities();
    IProcessInstanceHistoryLogQuery IncludeVariables();
    IProcessInstanceHistoryLogQuery IncludeTasks();
    Task<List<HistoricData>> ListAsync(CancellationToken cancellationToken = default);
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Services;

public interface IHistoricVariableInstanceQuery
{
    IHistoricVariableInstanceQuery ProcessInstanceId(string processInstanceId);
    IHistoricVariableInstanceQuery VariableName(string variableName);
    IHistoricVariableInstanceQuery TaskId(string taskId);
    Task<List<HistoricVariableInstance>> ListAsync(CancellationToken cancellationToken = default);
    Task<long> CountAsync(CancellationToken cancellationToken = default);
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Services;

public interface IHistoricDetailQuery
{
    IHistoricDetailQuery ProcessInstanceId(string processInstanceId);
    IHistoricDetailQuery VariableName(string variableName);
    IHistoricDetailQuery TaskId(string taskId);
    Task<List<HistoricDetail>> ListAsync(CancellationToken cancellationToken = default);
    Task<long> CountAsync(CancellationToken cancellationToken = default);
}

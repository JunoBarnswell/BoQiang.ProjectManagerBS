using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Api.Enums.Workflow.Runtime;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public interface IExtendProcinstService
{
    Task DeleteExtendProcinstByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task SaveExtendProcinstAndHisAsync(ExtendProcinst extendProcinst, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(ProcessStatusEnum processStatus, string processInstanceId, CancellationToken cancellationToken = default);
    Task<ExtendProcinst?> FindExtendProcinstByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default);
}

using AsterERP.Workflow.Approval.Api.Models.Workflow;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public interface IExtendHisprocinstService
{
    Task<ExtendHisprocinst?> FindExtendHisprocinstByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task UpdateAllStatusByProcessInstanceIdAsync(ExtendHisprocinst extendHisprocinst, CancellationToken cancellationToken = default);
}

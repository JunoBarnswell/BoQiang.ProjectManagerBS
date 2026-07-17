using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Runtime;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Forms.Core.Services.Form;

public interface IFormFlowOperationService
{
    Task<ReturnVo<string>> StartFormFlowAsync(StartProcessInstanceVo @params, CancellationToken cancellationToken = default);
}

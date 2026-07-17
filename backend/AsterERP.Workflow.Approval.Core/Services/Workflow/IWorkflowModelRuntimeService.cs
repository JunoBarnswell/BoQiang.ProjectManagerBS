using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public interface IWorkflowModelRuntimeService
{
    Task<ReturnVo<dynamic>> CreateModelAsync(dynamic modelRepresentation, User user, CancellationToken cancellationToken = default);
    Task<dynamic> ImportDecisionServiceModelAsync(string modelId, Stream file, User user, CancellationToken cancellationToken = default);
    Task<dynamic> DuplicateModelAsync(string modelId, dynamic modelRepresentation, User user, CancellationToken cancellationToken = default);
}

using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Model;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public interface IWorkflowBpmnDefinitionService
{
    Task<ReturnVo<string>> ValidateBpmnModelAsync(string modelId, string fileName, Stream modelStream, CancellationToken cancellationToken = default);
    Task<ReturnVo<string>> ImportBpmnModelAsync(string modelId, string fileName, Stream modelStream, User user, CancellationToken cancellationToken = default);
    Task<ReturnVo<string>> PublishBpmnAsync(string modelId, CancellationToken cancellationToken = default);
    Task<ReturnVo<dynamic>> DeployBpmnAsync(ModelInfo modelInfo, CancellationToken cancellationToken = default);
    Task<ReturnVo<dynamic>> CreateInitBpmnAsync(ModelInfo modelInfo, User user, CancellationToken cancellationToken = default);
    Task<ModelInfoVo?> LoadBpmnXmlByModelIdAsync(string modelId, CancellationToken cancellationToken = default);
    Task<ModelInfoVo?> LoadBpmnXmlByModelKeyAsync(string modelKey, CancellationToken cancellationToken = default);
    Task<ReturnVo<string>> StopBpmnAsync(string modelId, CancellationToken cancellationToken = default);
}

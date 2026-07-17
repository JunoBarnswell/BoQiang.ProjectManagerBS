using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Engine;
using CoreIntegrationContextEntity = AsterERP.Workflow.Core.Integration.IntegrationContextEntity;

namespace AsterERP.Workflow.Core.Services;

public interface IWorkflowPersistenceStore
{
    bool IsEnabled { get; }
    bool HasActiveTransaction { get; }

    Task InitializeAsync(IProcessEngineConfiguration processEngineConfiguration, CancellationToken cancellationToken = default);
    Task<string?> FindProcessInstanceIdByExecutionIdAsync(string executionId, CancellationToken cancellationToken = default);
    Task<AsterERP.Workflow.Core.Execution.ExecutionEntity?> LoadExecutionTreeAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task PersistRuntimeStateAsync(RuntimePersistenceBatch batch, CancellationToken cancellationToken = default);
    Task DeleteProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    Task PersistDeploymentAsync(
        DeploymentResult deployment,
        IReadOnlyDictionary<string, byte[]> resources,
        CancellationToken cancellationToken = default);

    Task PersistProcessDefinitionAsync(
        AsterERP.Workflow.Core.Deployer.ProcessDefinitionInfo processDefinition,
        AsterERP.Workflow.BpmnModel.BpmnModel bpmnModel,
        AsterERP.Workflow.BpmnModel.Process process,
        CancellationToken cancellationToken = default);

    Task DeleteDeploymentAsync(string deploymentId, CancellationToken cancellationToken = default);

    Task<ExecutionRecord?> GetExecutionAsync(string executionId, CancellationToken cancellationToken = default);
    Task<List<ExecutionRecord>> GetExecutionsAsync(CancellationToken cancellationToken = default);
    Task<List<ExecutionRecord>> GetExecutionsByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task<List<VariableInstanceRecord>> GetExecutionVariableInstancesAsync(string? executionId = null, CancellationToken cancellationToken = default);
    Task<List<string>> FindExecutionIdsBySignalSubscriptionAsync(string signalName, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<bool> HasMessageSubscriptionAsync(string executionId, string messageName, CancellationToken cancellationToken = default);
    Task<CoreIntegrationContextEntity?> GetIntegrationContextAsync(string id, CancellationToken cancellationToken = default);
    Task UpsertIntegrationContextAsync(CoreIntegrationContextEntity integrationContext, CancellationToken cancellationToken = default);
    Task DeleteIntegrationContextAsync(string id, CancellationToken cancellationToken = default);

    Task<TaskImplementation?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default);
    Task<List<TaskImplementation>> GetTasksAsync(CancellationToken cancellationToken = default);
    Task<List<TaskImplementation>> GetTasksAssignedToUserAsync(string userId, CancellationToken cancellationToken = default);
    Task<List<TaskImplementation>> GetTasksByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task<List<TaskImplementation>> GetSubTasksAsync(string parentTaskId, CancellationToken cancellationToken = default);

    Task<List<IdentityLinkEntity>> GetIdentityLinksForTaskAsync(string taskId, CancellationToken cancellationToken = default);
    Task<List<IdentityLinkEntity>> GetIdentityLinksForProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task<List<IdentityLinkEntity>> GetIdentityLinksForProcessDefinitionAsync(string processDefinitionId, CancellationToken cancellationToken = default);

    Task<AttachmentEntity?> GetAttachmentAsync(string attachmentId, CancellationToken cancellationToken = default);
    Task<byte[]?> GetAttachmentContentAsync(string attachmentId, CancellationToken cancellationToken = default);
    Task<List<AttachmentEntity>> GetAttachmentsAsync(CancellationToken cancellationToken = default);
    Task<List<AttachmentEntity>> GetTaskAttachmentsAsync(string taskId, CancellationToken cancellationToken = default);
    Task<List<AttachmentEntity>> GetProcessInstanceAttachmentsAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task PersistAttachmentAsync(AttachmentEntity attachment, byte[]? content = null, CancellationToken cancellationToken = default);
    Task DeleteAttachmentAsync(string attachmentId, CancellationToken cancellationToken = default);

    Task<List<CommentEntity>> GetCommentsAsync(CancellationToken cancellationToken = default);
    Task<CommentEntity?> GetCommentAsync(string commentId, CancellationToken cancellationToken = default);
    Task<List<CommentEntity>> GetTaskCommentsAsync(string taskId, string? type = null, CancellationToken cancellationToken = default);
    Task<List<CommentEntity>> GetCommentsByTypeAsync(string type, CancellationToken cancellationToken = default);
    Task<List<CommentEntity>> GetProcessInstanceCommentsAsync(string processInstanceId, string? type = null, CancellationToken cancellationToken = default);
    Task PersistCommentAsync(CommentEntity comment, CancellationToken cancellationToken = default);
    Task DeleteCommentAsync(string commentId, CancellationToken cancellationToken = default);

    Task<List<HistoricProcessInstance>> GetHistoricProcessInstancesAsync(CancellationToken cancellationToken = default);
    Task<HistoricProcessInstance?> GetHistoricProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task<List<HistoricTaskInstance>> GetHistoricTaskInstancesAsync(CancellationToken cancellationToken = default);
    Task<List<HistoricActivityInstance>> GetHistoricActivityInstancesAsync(CancellationToken cancellationToken = default);
    Task<List<HistoricVariableInstance>> GetHistoricVariableInstancesAsync(CancellationToken cancellationToken = default);
    Task<List<HistoricDetail>> GetHistoricDetailsAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task<List<HistoricIdentityLink>> GetHistoricIdentityLinksAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task<List<IdentityLinkEntity>> GetHistoricIdentityLinksForProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task<List<IdentityLinkEntity>> GetHistoricIdentityLinksForTaskAsync(string taskId, CancellationToken cancellationToken = default);
    Task DeleteHistoricProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task DeleteHistoricTaskInstanceAsync(string taskId, CancellationToken cancellationToken = default);
}

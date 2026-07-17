using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Core.Variable;

namespace AsterERP.Workflow.Core.Service;

public class TaskServiceImplementation : ServiceImpl, ITaskService
{
    public TaskServiceImplementation() : base(AsterERP.Workflow.Core.Engine.ProcessEngineConfiguration.CreateDefault()) { }

    public TaskServiceImplementation(IProcessEngineConfiguration processEngineConfiguration)
        : base(processEngineConfiguration) { }

    public TaskServiceImplementation(ICommandExecutor commandExecutor)
        : base(commandExecutor) { }

    public async Task<TaskImplementation?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
return await CommandExecutor.ExecuteAsync(new GetTaskByIdCmd(taskId), cancellationToken);
    }

    public async Task<TaskImplementation> CreateTaskAsync(TaskImplementation task, CancellationToken cancellationToken = default)
    {
        var created = await CommandExecutor.ExecuteAsync(new NewTaskCmd(task), cancellationToken);
        var persisted = await CommandExecutor.ExecuteAsync(new SaveTaskCmd(created), cancellationToken);

        return persisted;
    }

    public async Task<TaskImplementation> SaveTaskAsync(TaskImplementation task, CancellationToken cancellationToken = default)
    {
        var saved = await CommandExecutor.ExecuteAsync(new SaveTaskCmd(task), cancellationToken);

        return saved;
    }

    public async Task DeleteTaskAsync(string taskId, string? deleteReason = null, bool cascade = false, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new DeleteTaskCmd(taskId, deleteReason, cascade), cancellationToken);

    }

    public async Task DeleteTasksAsync(ICollection<string> taskIds, string? deleteReason = null, bool cascade = false, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new DeleteTasksCmd(taskIds, deleteReason, cascade), cancellationToken);

    }

    public async Task CompleteTaskAsync(string taskId, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new CompleteTaskCmd(taskId, variables), cancellationToken);

    }

    public async Task CompleteTaskAsync(string taskId, Dictionary<string, object?>? variables, Dictionary<string, object?>? transientVariables, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new CompleteTaskWithTransientCmd(taskId, variables, transientVariables), cancellationToken);

    }

    public async Task DelegateTaskAsync(string taskId, string userId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new DelegateTaskCmd(taskId, userId), cancellationToken);

    }

    public async Task ResolveTaskAsync(string taskId, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new ResolveTaskCmd(taskId, variables), cancellationToken);

    }

    public async Task ResolveTaskAsync(string taskId, Dictionary<string, object?>? variables, Dictionary<string, object?>? transientVariables, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new ResolveTaskWithTransientCmd(taskId, variables, transientVariables), cancellationToken);

    }

    public async Task SetAssigneeAsync(string taskId, string userId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new AddIdentityLinkCmd(taskId, userId, null, IdentityLinkType.ASSIGNEE), cancellationToken);

    }

    public async Task SetOwnerAsync(string taskId, string userId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new AddIdentityLinkCmd(taskId, userId, null, IdentityLinkType.OWNER), cancellationToken);

    }

    public async Task ClaimTaskAsync(string taskId, string userId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new ClaimTaskCmd(taskId, userId), cancellationToken);

    }

    public async Task UnclaimTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new ClaimTaskCmd(taskId, null), cancellationToken);

    }

    public async Task SetPriorityAsync(string taskId, int priority, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new SetTaskPriorityCmd(taskId, priority), cancellationToken);

    }

    public async Task SetDueDateAsync(string taskId, DateTime? dueDate, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new SetTaskDueDateCmd(taskId, dueDate), cancellationToken);

    }

    public async Task<List<TaskImplementation>> GetTasksAsync(CancellationToken cancellationToken = default)
    {
return await CommandExecutor.ExecuteAsync(new GetAllTasksCmd(), cancellationToken);
    }

    public async Task<List<TaskImplementation>> GetTasksAssignedToUserAsync(string userId, CancellationToken cancellationToken = default)
    {
return await CommandExecutor.ExecuteAsync(new GetTasksByAssigneeCmd(userId), cancellationToken);
    }

    public async Task<List<TaskImplementation>> GetTasksByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
return await CommandExecutor.ExecuteAsync(new GetTasksByProcessInstanceCmd(processInstanceId), cancellationToken);
    }

    public async Task<object?> GetVariableAsync(string taskId, string variableName, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetTaskVariableCmd(taskId, variableName, false), cancellationToken);
    }

    public async Task<T?> GetVariableAsync<T>(string taskId, string variableName, CancellationToken cancellationToken = default)
    {
        var value = await GetVariableAsync(taskId, variableName, cancellationToken);
        if (value == null) return default;
        return (T)value;
    }

    public async Task<object?> GetVariableLocalAsync(string taskId, string variableName, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetTaskVariableCmd(taskId, variableName, true), cancellationToken);
    }

    public async Task<Dictionary<string, object?>> GetVariablesAsync(string taskId, ICollection<string>? variableNames = null, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetTaskVariablesCmd(taskId, false), cancellationToken);
    }

    public async Task<Dictionary<string, object?>> GetVariablesLocalAsync(string taskId, ICollection<string>? variableNames = null, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetTaskVariablesCmd(taskId, true), cancellationToken);
    }

    public async Task<VariableInstanceEntity?> GetVariableInstanceAsync(string taskId, string variableName, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetTaskVariableInstanceCmd(taskId, variableName, false), cancellationToken);
    }

    public async Task<VariableInstanceEntity?> GetVariableInstanceLocalAsync(string taskId, string variableName, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetTaskVariableInstanceCmd(taskId, variableName, true), cancellationToken);
    }

    public async Task<Dictionary<string, VariableInstanceEntity>> GetVariableInstancesAsync(string taskId, ICollection<string>? variableNames = null, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetTaskVariableInstancesCmd(taskId, variableNames, false), cancellationToken);
    }

    public async Task<Dictionary<string, VariableInstanceEntity>> GetVariableInstancesLocalAsync(string taskId, ICollection<string>? variableNames = null, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetTaskVariableInstancesCmd(taskId, variableNames, true), cancellationToken);
    }

    public async Task<bool> HasVariableAsync(string taskId, string variableName, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new HasTaskVariableCmd(taskId, variableName, false), cancellationToken);
    }

    public async Task<bool> HasVariableLocalAsync(string taskId, string variableName, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new HasTaskVariableCmd(taskId, variableName, true), cancellationToken);
    }

    public async Task SetVariableAsync(string taskId, string variableName, object? value, CancellationToken cancellationToken = default)
    {
        if (variableName == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("variableName is null");
        var variables = new Dictionary<string, object?> { [variableName] = value };
        await CommandExecutor.ExecuteAsync(new SetTaskVariablesCmd(taskId, variables, false), cancellationToken);
    }

    public async Task SetVariableLocalAsync(string taskId, string variableName, object? value, CancellationToken cancellationToken = default)
    {
        if (variableName == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("variableName is null");
        var variables = new Dictionary<string, object?> { [variableName] = value };
        await CommandExecutor.ExecuteAsync(new SetTaskVariablesCmd(taskId, variables, true), cancellationToken);
    }

    public async Task SetVariablesAsync(string taskId, Dictionary<string, object?> variables, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new SetTaskVariablesCmd(taskId, variables, false), cancellationToken);
    }

    public async Task SetVariablesLocalAsync(string taskId, Dictionary<string, object?> variables, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new SetTaskVariablesCmd(taskId, variables, true), cancellationToken);
    }

    public async Task RemoveVariableAsync(string taskId, string variableName, CancellationToken cancellationToken = default)
    {
        var variableNames = new List<string> { variableName };
        await CommandExecutor.ExecuteAsync(new RemoveTaskVariablesCmd(taskId, variableNames, false), cancellationToken);
    }

    public async Task RemoveVariableLocalAsync(string taskId, string variableName, CancellationToken cancellationToken = default)
    {
        var variableNames = new List<string> { variableName };
        await CommandExecutor.ExecuteAsync(new RemoveTaskVariablesCmd(taskId, variableNames, true), cancellationToken);
    }

    public async Task RemoveVariablesAsync(string taskId, ICollection<string> variableNames, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new RemoveTaskVariablesCmd(taskId, variableNames, false), cancellationToken);
    }

    public async Task RemoveVariablesLocalAsync(string taskId, ICollection<string> variableNames, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new RemoveTaskVariablesCmd(taskId, variableNames, true), cancellationToken);
    }

    public async Task<CommentEntity> AddCommentAsync(string taskId, string? processInstanceId, string message, CancellationToken cancellationToken = default)
    {
        var comment = await CommandExecutor.ExecuteAsync(new AddCommentCmd(taskId, processInstanceId, message), cancellationToken);
return comment;
    }

    public async Task<CommentEntity> AddCommentAsync(string taskId, string? processInstanceId, string? type, string message, CancellationToken cancellationToken = default)
    {
        var comment = await CommandExecutor.ExecuteAsync(new AddCommentCmd(taskId, processInstanceId, type, message), cancellationToken);
return comment;
    }

    public async Task<CommentEntity?> GetCommentAsync(string commentId, CancellationToken cancellationToken = default)
    {
return await CommandExecutor.ExecuteAsync(new GetCommentCmd(commentId), cancellationToken);
    }

    public async Task DeleteCommentAsync(string commentId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new DeleteCommentCmd(commentId), cancellationToken);
}

    public async Task<List<CommentEntity>> GetTaskCommentsAsync(string taskId, CancellationToken cancellationToken = default)
    {
return await CommandExecutor.ExecuteAsync(new GetTaskCommentsCmd(taskId), cancellationToken);
    }

    public async Task<List<CommentEntity>> GetTaskCommentsAsync(string taskId, string type, CancellationToken cancellationToken = default)
    {
return await CommandExecutor.ExecuteAsync(new GetTaskCommentsByTypeCmd(taskId, type), cancellationToken);
    }

    public async Task<List<CommentEntity>> GetCommentsByTypeAsync(string type, CancellationToken cancellationToken = default)
    {
return await CommandExecutor.ExecuteAsync(new GetTypeCommentsCmd(type), cancellationToken);
    }

    public async Task<List<CommentEntity>> GetProcessInstanceCommentsAsync(string processInstanceId, string? type = null, CancellationToken cancellationToken = default)
    {
return await CommandExecutor.ExecuteAsync(new GetProcessInstanceCommentsCmd(processInstanceId, type), cancellationToken);
    }

    public async Task<List<EventEntity>> GetTaskEventsAsync(string taskId, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetTaskEventsCmd(taskId), cancellationToken);
    }

    public async Task<EventEntity?> GetEventAsync(string eventId, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetTaskEventCmd(eventId), cancellationToken);
    }

    public async Task<AttachmentEntity> CreateAttachmentAsync(
        string? attachmentType, string? taskId, string? processInstanceId,
        string? attachmentName, string? attachmentDescription, byte[]? content, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(
            new CreateAttachmentCmd(attachmentType, taskId, processInstanceId, attachmentName, attachmentDescription, content, null), cancellationToken);
    }

    public async Task<AttachmentEntity> CreateAttachmentAsync(
        string? attachmentType, string? taskId, string? processInstanceId,
        string? attachmentName, string? attachmentDescription, string? url, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(
            new CreateAttachmentCmd(attachmentType, taskId, processInstanceId, attachmentName, attachmentDescription, null, url), cancellationToken);
    }

    public async Task<AttachmentEntity?> GetAttachmentAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetAttachmentCmd(attachmentId), cancellationToken);
    }

    public async Task<byte[]?> GetAttachmentContentAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetAttachmentContentCmd(attachmentId), cancellationToken);
    }

    public async Task<List<AttachmentEntity>> GetTaskAttachmentsAsync(string taskId, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetTaskAttachmentsCmd(taskId), cancellationToken);
    }

    public async Task<List<AttachmentEntity>> GetProcessInstanceAttachmentsAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetProcessInstanceAttachmentsCmd(processInstanceId), cancellationToken);
    }

    public async Task SaveAttachmentAsync(AttachmentEntity attachment, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new SaveAttachmentCmd(attachment), cancellationToken);
    }

    public async Task DeleteAttachmentAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new DeleteAttachmentCmd(attachmentId), cancellationToken);
    }

    public async Task AddCandidateUserAsync(string taskId, string userId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new AddIdentityLinkCmd(taskId, userId, AddIdentityLinkCmd.IDENTITY_USER, IdentityLinkType.CANDIDATE), cancellationToken);

    }

    public async Task AddCandidateGroupAsync(string taskId, string groupId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new AddIdentityLinkCmd(taskId, groupId, AddIdentityLinkCmd.IDENTITY_GROUP, IdentityLinkType.CANDIDATE), cancellationToken);

    }

    public async Task AddUserIdentityLinkAsync(string taskId, string userId, string identityLinkType, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new AddIdentityLinkCmd(taskId, userId, AddIdentityLinkCmd.IDENTITY_USER, identityLinkType), cancellationToken);

    }

    public async Task AddGroupIdentityLinkAsync(string taskId, string groupId, string identityLinkType, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new AddIdentityLinkCmd(taskId, groupId, AddIdentityLinkCmd.IDENTITY_GROUP, identityLinkType), cancellationToken);

    }

    public async Task DeleteCandidateUserAsync(string taskId, string userId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new DeleteIdentityLinkCmd(taskId, userId, null, IdentityLinkType.CANDIDATE), cancellationToken);

    }

    public async Task DeleteCandidateGroupAsync(string taskId, string groupId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new DeleteIdentityLinkCmd(taskId, null, groupId, IdentityLinkType.CANDIDATE), cancellationToken);

    }

    public async Task DeleteUserIdentityLinkAsync(string taskId, string userId, string identityLinkType, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new DeleteIdentityLinkCmd(taskId, userId, null, identityLinkType), cancellationToken);

    }

    public async Task DeleteGroupIdentityLinkAsync(string taskId, string groupId, string identityLinkType, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new DeleteIdentityLinkCmd(taskId, null, groupId, identityLinkType), cancellationToken);

    }

    public async Task<List<IdentityLinkEntity>> GetIdentityLinksForTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
return await CommandExecutor.ExecuteAsync(new GetIdentityLinksForTaskCmd(taskId), cancellationToken);
    }

    public async Task<List<TaskImplementation>> GetSubTasksAsync(string parentTaskId, CancellationToken cancellationToken = default)
    {
return await CommandExecutor.ExecuteAsync(new GetSubTasksCmd(parentTaskId), cancellationToken);
    }

    public async Task<Dictionary<string, DataObjectImpl>> GetDataObjectsAsync(string taskId, ICollection<string>? dataObjectNames = null, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetTaskDataObjectsCmd(taskId, dataObjectNames), cancellationToken);
    }

    public async Task<DataObjectImpl?> GetDataObjectAsync(string taskId, string dataObjectName, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetTaskDataObjectCmd(taskId, dataObjectName), cancellationToken);
    }
}






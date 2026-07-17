using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Context;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class ModelEntity
{
    public string Id { get; set; } = AbpTimeIdProvider.NewGuid("N");
    public string? Name { get; set; }
    public string? Category { get; set; }
    public string? Key { get; set; }
    public string? Description { get; set; }
    public int Version { get; set; } = 1;
    public string? MetaInfo { get; set; }
    public string? DeploymentId { get; set; }
    public string? EditorSourceValueId { get; set; }
    public string? EditorSourceExtraValueId { get; set; }
    public DateTime CreatedTime { get; set; } = AbpTimeIdProvider.UtcNow;
    public DateTime? LastUpdatedTime { get; set; }
    public string? TenantId { get; set; }
}

public class CreateModelCmd : ICommand<ModelEntity>
{
    public ModelEntity Execute(ICommandContext context) => throw new NotSupportedException("CreateModelCmd is async-only. Use ExecuteAsync.");

    public Task<ModelEntity> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ModelEntity());
    }
}

public class SaveModelCmd : ICommand<object?>
{
    private readonly ModelEntity _model;

    public SaveModelCmd(ModelEntity model)
    {
        _model = model ?? throw new WorkflowEngineArgumentException("model is null");
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("SaveModelCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
            await eventDispatcher.DispatchEventAsync(WorkflowEventBuilder.CreateEntityEvent(WorkflowEventType.ENTITY_UPDATED, _model), cancellationToken);
        return null;
    }
}

public class DeleteModelCmd : ICommand<object?>
{
    private readonly string _modelId;

    public DeleteModelCmd(string modelId)
    {
        _modelId = modelId;
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("DeleteModelCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_modelId))
            throw new WorkflowEngineArgumentException("modelId is null");
        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
            await eventDispatcher.DispatchEventAsync(WorkflowEventBuilder.CreateEntityEvent(WorkflowEventType.ENTITY_DELETED, new { ModelId = _modelId }), cancellationToken);
        return null;
    }
}

public class GetModelCmd : ICommand<ModelEntity?>
{
    private readonly string _modelId;

    public GetModelCmd(string modelId)
    {
        _modelId = modelId;
    }

    public ModelEntity? Execute(ICommandContext context) => throw new NotSupportedException("GetModelCmd is async-only. Use ExecuteAsync.");

    public Task<ModelEntity?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_modelId)) throw new WorkflowEngineArgumentException("modelId is null");
        return Task.FromResult<ModelEntity?>(null);
    }
}

public class GetModelEditorSourceCmd : ICommand<byte[]?>
{
    private readonly string _modelId;

    public GetModelEditorSourceCmd(string modelId)
    {
        _modelId = modelId;
    }

    public byte[]? Execute(ICommandContext context) => throw new NotSupportedException("GetModelEditorSourceCmd is async-only. Use ExecuteAsync.");

    public Task<byte[]?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_modelId)) throw new WorkflowEngineArgumentException("modelId is null");
        return Task.FromResult<byte[]?>(null);
    }
}

public class GetModelEditorSourceExtraCmd : ICommand<byte[]?>
{
    private readonly string _modelId;

    public GetModelEditorSourceExtraCmd(string modelId)
    {
        _modelId = modelId;
    }

    public byte[]? Execute(ICommandContext context) => throw new NotSupportedException("GetModelEditorSourceExtraCmd is async-only. Use ExecuteAsync.");

    public Task<byte[]?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_modelId)) throw new WorkflowEngineArgumentException("modelId is null");
        return Task.FromResult<byte[]?>(null);
    }
}

public class AddEditorSourceForModelCmd : ICommand<object?>
{
    private readonly string _modelId;
    private readonly byte[] _editorSource;

    public AddEditorSourceForModelCmd(string modelId, byte[] editorSource)
    {
        _modelId = modelId;
        _editorSource = editorSource;
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("AddEditorSourceForModelCmd is async-only. Use ExecuteAsync.");

    public Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_modelId)) throw new WorkflowEngineArgumentException("modelId is null");
        return Task.FromResult<object?>(null);
    }
}

public class AddEditorSourceExtraForModelCmd : ICommand<object?>
{
    private readonly string _modelId;
    private readonly byte[] _editorSourceExtra;

    public AddEditorSourceExtraForModelCmd(string modelId, byte[] editorSourceExtra)
    {
        _modelId = modelId;
        _editorSourceExtra = editorSourceExtra;
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("AddEditorSourceExtraForModelCmd is async-only. Use ExecuteAsync.");

    public Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_modelId)) throw new WorkflowEngineArgumentException("modelId is null");
        return Task.FromResult<object?>(null);
    }
}

public class CreateAttachmentCmd : ICommand<AttachmentEntity>
{
    private readonly string? _attachmentType;
    private readonly string? _taskId;
    private readonly string? _processInstanceId;
    private readonly string? _attachmentName;
    private readonly string? _attachmentDescription;
    private readonly byte[]? _content;
    private readonly string? _url;

    public CreateAttachmentCmd(
        string? attachmentType,
        string? taskId,
        string? processInstanceId,
        string? attachmentName,
        string? attachmentDescription,
        byte[]? content,
        string? url)
    {
        _attachmentType = attachmentType;
        _taskId = taskId;
        _processInstanceId = processInstanceId;
        _attachmentName = attachmentName;
        _attachmentDescription = attachmentDescription;
        _content = content;
        _url = url;
    }


    public async Task<AttachmentEntity> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        await ValidateTaskAndProcessInstanceAsync(context, _taskId, _processInstanceId, cancellationToken);

        var attachment = new AttachmentEntity
        {
            Name = _attachmentName,
            ProcessInstanceId = _processInstanceId,
            TaskId = _taskId,
            Description = _attachmentDescription,
            Type = _attachmentType,
            Url = _url,
            Time = AbpTimeIdProvider.UtcNow
        };

        context.SaveAttachment(attachment, _content);
        CreateAttachmentComment(context, attachment, true);

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            await eventDispatcher.DispatchEventAsync(
                WorkflowEventBuilder.CreateEntityEvent(WorkflowEventType.ENTITY_CREATED, attachment, processInstanceId: _processInstanceId), cancellationToken);

            await eventDispatcher.DispatchEventAsync(
                WorkflowEventBuilder.CreateEntityEvent(WorkflowEventType.ENTITY_INITIALIZED, attachment, processInstanceId: _processInstanceId), cancellationToken);
        }

        return attachment;
    }

    internal static async Task ValidateTaskAndProcessInstanceAsync(ICommandContext context, string? taskId, string? processInstanceId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(taskId))
        {
            var execution = await context.FindExecutionByTaskIdAsync(taskId, cancellationToken);
            if (execution == null)
                throw new WorkflowEngineObjectNotFoundException($"Cannot find task with id '{taskId}'", typeof(TaskImplementation));
            if (!execution.IsActive)
                throw new WorkflowEngineException($"Cannot operate on task '{taskId}' because its execution is suspended");
        }

        if (!string.IsNullOrEmpty(processInstanceId))
        {
            var matchingExecutionRecords = await ResolveMatchingExecutionRecordsAsync(context, processInstanceId, cancellationToken);

            var execution = await context.GetCurrentExecutionAsync(processInstanceId, cancellationToken)
                ?? matchingExecutionRecords
                    .OrderByDescending(IsProcessInstanceRecord)
                    .ThenByDescending(record => record.IsActive)
                    .Select(ToExecutionEntity)
                    .FirstOrDefault();
            if (execution == null)
            {
                if (!string.IsNullOrEmpty(taskId))
                {
                    return;
                }

                throw new WorkflowEngineObjectNotFoundException($"Cannot find process instance with id '{processInstanceId}'", typeof(ExecutionEntity));
            }
            if (!matchingExecutionRecords.Any(record => record.IsActive) && !HasAnyActiveExecution(execution))
                throw new WorkflowEngineException($"Cannot operate on process instance '{processInstanceId}' because it is suspended");
        }
    }

    private static async Task<List<ExecutionRecord>> ResolveMatchingExecutionRecordsAsync(ICommandContext context, string processInstanceId, CancellationToken cancellationToken)
    {
        var store = ProcessEngineServiceProviderAccessor.GetService<IWorkflowPersistenceStore>(context.ProcessEngineConfiguration);
        if (store?.IsEnabled == true)
        {
            return await store.GetExecutionsByProcessInstanceIdAsync(processInstanceId, cancellationToken);
        }

        return (await context.FindExecutionsAsync(null, cancellationToken))
            .Where(execution => execution.Id == processInstanceId || execution.ProcessInstanceId == processInstanceId)
            .Select(execution => new ExecutionRecord
            {
                Id = execution.Id,
                ProcessInstanceId = execution.ProcessInstanceId,
                ProcessDefinitionId = execution.ProcessDefinitionId,
                ParentId = execution.ParentId,
                CurrentActivityId = execution.CurrentFlowElementId ?? execution.ActivityId ?? execution.CurrentActivityId,
                CurrentActivityName = execution.CurrentFlowElement?.Name ?? execution.CurrentActivityName,
                IsActive = execution.IsActive,
                IsEnded = execution.IsEnded,
                BusinessKey = execution.BusinessKey
            })
            .ToList();
    }

    private static bool IsProcessInstanceRecord(ExecutionRecord record)
    {
        return string.Equals(record.Id, record.ProcessInstanceId, StringComparison.Ordinal)
               || string.IsNullOrWhiteSpace(record.ParentId);
    }

    private static ExecutionEntity ToExecutionEntity(ExecutionRecord record)
    {
        return new ExecutionEntity
        {
            Id = record.Id,
            ProcessInstanceId = record.ProcessInstanceId ?? record.Id,
            ProcessDefinitionId = record.ProcessDefinitionId,
            ParentId = record.ParentId,
            CurrentActivityId = record.CurrentActivityId,
            CurrentFlowElementId = record.CurrentActivityId,
            CurrentActivityName = record.CurrentActivityName,
            IsActive = record.IsActive,
            IsEnded = record.IsEnded,
            BusinessKey = record.BusinessKey
        };
    }

    private static bool HasAnyActiveExecution(ExecutionEntity execution)
    {
        if (execution.IsActive)
        {
            return true;
        }

        foreach (var childExecution in execution.ChildExecutions)
        {
            if (HasAnyActiveExecution(childExecution))
            {
                return true;
            }
        }

        return false;
    }

    internal static void CreateAttachmentComment(ICommandContext context, AttachmentEntity attachment, bool create) => throw new NotSupportedException("Attachment comments are async-only. Use CreateAttachmentCommentAsync.");

    internal static Task CreateAttachmentCommentAsync(ICommandContext context, AttachmentEntity attachment, bool create, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(attachment.TaskId))
            return Task.CompletedTask;

        context.AddComment(new CommentEntity
        {
            Type = CommentEntity.TYPE_EVENT,
            Time = AbpTimeIdProvider.UtcNow,
            TaskId = attachment.TaskId,
            ProcessInstanceId = attachment.ProcessInstanceId,
            Action = create ? "AddAttachment" : "DeleteAttachment",
            Message = attachment.Name,
            FullMessage = attachment.Name
        });
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

public class SaveAttachmentCmd : ICommand<object?>
{
    private readonly AttachmentEntity _attachment;

    public SaveAttachmentCmd(AttachmentEntity attachment)
    {
        _attachment = attachment ?? throw new ArgumentNullException(nameof(attachment));
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("SaveAttachmentCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var existing = (await context.FindAttachmentsAsync(a => a.Id == _attachment.Id, cancellationToken)).FirstOrDefault();
        if (existing != null)
        {
            existing.Name = _attachment.Name;
            existing.Description = _attachment.Description;
            context.SaveAttachment(existing);
        }
        else
        {
            context.SaveAttachment(_attachment);
        }

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
            await eventDispatcher.DispatchEventAsync(WorkflowEventBuilder.CreateEntityEvent(WorkflowEventType.ENTITY_UPDATED, _attachment), cancellationToken);
        return null;
    }
}

public class DeleteAttachmentCmd : ICommand<object?>
{
    private readonly string _attachmentId;

    public DeleteAttachmentCmd(string attachmentId)
    {
        _attachmentId = attachmentId;
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("DeleteAttachmentCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(_attachmentId))
            throw new WorkflowEngineArgumentException("attachmentId is null");
        var attachment = (await context.FindAttachmentsAsync(a => a.Id == _attachmentId, cancellationToken)).FirstOrDefault();
        if (attachment == null)
            throw new WorkflowEngineObjectNotFoundException($"Cannot find attachment with id '{_attachmentId}'", typeof(AttachmentEntity));
        context.DeleteAttachment(_attachmentId);
        await CreateAttachmentCmd.CreateAttachmentCommentAsync(context, attachment, false, cancellationToken);
        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
            await eventDispatcher.DispatchEventAsync(WorkflowEventBuilder.CreateEntityEvent(WorkflowEventType.ENTITY_DELETED, attachment), cancellationToken);
        return null;
    }
}

public class GetAttachmentCmd : ICommand<AttachmentEntity?>
{
    private readonly string _attachmentId;

    public GetAttachmentCmd(string attachmentId)
    {
        _attachmentId = attachmentId;
    }

    public AttachmentEntity? Execute(ICommandContext context) => throw new NotSupportedException("GetAttachmentCmd is async-only. Use ExecuteAsync.");

    public async Task<AttachmentEntity?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_attachmentId))
            throw new WorkflowEngineArgumentException("attachmentId is null");
        return (await context.FindAttachmentsAsync(a => a.Id == _attachmentId, cancellationToken)).FirstOrDefault();
    }
}

public class GetAttachmentContentCmd : ICommand<byte[]?>
{
    private readonly string _attachmentId;

    public GetAttachmentContentCmd(string attachmentId)
    {
        _attachmentId = attachmentId;
    }

    public byte[]? Execute(ICommandContext context) => throw new NotSupportedException("GetAttachmentContentCmd is async-only. Use ExecuteAsync.");

    public async Task<byte[]?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_attachmentId))
            throw new WorkflowEngineArgumentException("attachmentId is null");
        var attachment = (await context.FindAttachmentsAsync(a => a.Id == _attachmentId, cancellationToken)).FirstOrDefault();
        if (attachment == null || string.IsNullOrEmpty(attachment.ContentId))
            return null;
        return await context.GetAttachmentContentAsync(_attachmentId, cancellationToken);
    }
}

public class AddCommentCmd : ICommand<CommentEntity>
{
    private readonly string? _taskId;
    private readonly string? _processInstanceId;
    private readonly string? _type;
    private readonly string _message;

    public AddCommentCmd(string? taskId, string? processInstanceId, string message)
    {
        _taskId = taskId;
        _processInstanceId = processInstanceId;
        _message = message;
    }

    public AddCommentCmd(string? taskId, string? processInstanceId, string? type, string message)
    {
        _taskId = taskId;
        _processInstanceId = processInstanceId;
        _type = type;
        _message = message;
    }


    public async Task<CommentEntity> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        await CreateAttachmentCmd.ValidateTaskAndProcessInstanceAsync(context, _taskId, _processInstanceId, cancellationToken);

        var comment = new CommentEntity
        {
            Type = string.IsNullOrEmpty(_type) ? CommentEntity.TYPE_COMMENT : _type,
            Time = AbpTimeIdProvider.UtcNow,
            TaskId = _taskId,
            ProcessInstanceId = _processInstanceId,
            Action = "AddComment",
            FullMessage = _message
        };

        var eventMessage = Regex.Replace(_message ?? string.Empty, "\\s+", " ");
        if (eventMessage.Length > 163)
        {
            eventMessage = eventMessage.Substring(0, 160) + "...";
        }
        comment.Message = eventMessage;
        context.AddComment(comment);

        return comment;
    }

}

public class DeleteCommentCmd : ICommand<object?>
{
    private readonly string _commentId;

    public DeleteCommentCmd(string commentId)
    {
        _commentId = commentId;
    }

    public object? Execute(ICommandContext context) => throw new NotSupportedException("DeleteCommentCmd is async-only. Use ExecuteAsync.");

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_commentId))
            throw new WorkflowEngineArgumentException("commentId is null");
        if (!(await context.FindCommentsAsync(c => c.Id == _commentId, cancellationToken)).Any())
            throw new WorkflowEngineObjectNotFoundException($"Cannot find comment with id '{_commentId}'", typeof(CommentEntity));
        context.DeleteComment(_commentId);
        return null;
    }
}

public class GetCommentCmd : ICommand<CommentEntity?>
{
    private readonly string _commentId;

    public GetCommentCmd(string commentId)
    {
        _commentId = commentId;
    }

    public CommentEntity? Execute(ICommandContext context) => throw new NotSupportedException("GetCommentCmd is async-only. Use ExecuteAsync.");

    public async Task<CommentEntity?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_commentId))
            throw new WorkflowEngineArgumentException("commentId is null");
        return (await context.FindCommentsAsync(c => c.Id == _commentId, cancellationToken)).FirstOrDefault();
    }
}


using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.DependencyInjection.Persistence;

public sealed class SqlSugarCommandContextSession : ICommandContextSession
{
    private readonly IWorkflowPersistenceStore _store;

    public SqlSugarCommandContextSession(IWorkflowPersistenceStore store)
    {
        _store = store;
    }

    public bool HasActiveTransaction => _store.HasActiveTransaction;

    public void Flush()
    {
    }

    public void Close()
    {
    }

    public Task BeginAsync(CancellationToken cancellationToken = default)
    {
        return _store.BeginTransactionAsync(cancellationToken);
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        return _store.CommitTransactionAsync(cancellationToken);
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        return _store.RollbackTransactionAsync(cancellationToken);
    }

    public Task<string?> FindProcessInstanceIdByExecutionIdAsync(string executionId, CancellationToken cancellationToken = default)
    {
        return _store.FindProcessInstanceIdByExecutionIdAsync(executionId, cancellationToken);
    }

    public Task<ExecutionEntity?> LoadExecutionTreeAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        return _store.LoadExecutionTreeAsync(processInstanceId, cancellationToken);
    }

    public Task<TaskImplementation?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        return _store.GetTaskAsync(taskId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<TaskImplementation>> GetTasksAsync(CancellationToken cancellationToken = default)
    {
        return await _store.GetTasksAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ExecutionRecord>> GetExecutionsAsync(CancellationToken cancellationToken = default)
    {
        return await _store.GetExecutionsAsync(cancellationToken);
    }

    public Task<AttachmentEntity?> GetAttachmentAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        return _store.GetAttachmentAsync(attachmentId, cancellationToken);
    }

    public Task<byte[]?> GetAttachmentContentAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        return _store.GetAttachmentContentAsync(attachmentId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<AttachmentEntity>> GetAttachmentsAsync(CancellationToken cancellationToken = default)
    {
        return await _store.GetAttachmentsAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CommentEntity>> GetCommentsAsync(CancellationToken cancellationToken = default)
    {
        return await _store.GetCommentsAsync(cancellationToken);
    }

    public async Task FlushAsync(CommandContextFlushState state, CancellationToken cancellationToken = default)
    {
        foreach (var commentId in state.DeletedCommentIds)
        {
            await _store.DeleteCommentAsync(commentId, cancellationToken);
        }

        foreach (var comment in state.Comments)
        {
            await _store.PersistCommentAsync(comment, cancellationToken);
        }

        foreach (var attachmentId in state.DeletedAttachmentIds)
        {
            await _store.DeleteAttachmentAsync(attachmentId, cancellationToken);
        }

        foreach (var attachment in state.Attachments)
        {
            state.AttachmentContents.TryGetValue(attachment.Id, out var content);
            await _store.PersistAttachmentAsync(attachment, content, cancellationToken);
        }

        await _store.PersistRuntimeStateAsync(state.RuntimePersistenceBatch, cancellationToken);
    }
}

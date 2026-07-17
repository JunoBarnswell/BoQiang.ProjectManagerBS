using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Command;

public interface ICommand<T>
{
    T Execute(ICommandContext context) => throw new NotSupportedException("This command is async-only. Use ExecuteAsync.");
    Task<T> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default);
}

public interface ICommand : ICommand<object?>
{
}

public interface ICommandContext
{
    TSession GetSession<TSession>() where TSession : class, ISession;
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
    void AddCloseListener(ICommandContextCloseListener closeListener);
    IReadOnlyCollection<ICommandContextCloseListener> GetCloseListeners();
    bool IsClosed { get; }
    IProcessEngineConfiguration ProcessEngineConfiguration { get; }
    Task<ExecutionEntity?> GetCurrentExecutionAsync(string executionId, CancellationToken cancellationToken = default);
    Task<ExecutionEntity?> FindExecutionByTaskIdAsync(string taskId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ExecutionEntity>> FindExecutionsAsync(Func<ExecutionEntity, bool>? predicate = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CommentEntity>> FindCommentsAsync(Func<CommentEntity, bool>? predicate = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TaskImplementation>> FindTasksAsync(Func<TaskImplementation, bool>? predicate = null, CancellationToken cancellationToken = default);
    void AddComment(CommentEntity comment);
    void DeleteComment(string commentId);
    void SaveAttachment(AttachmentEntity attachment, byte[]? content = null);
    void DeleteAttachment(string attachmentId);
    Task<byte[]?> GetAttachmentContentAsync(string attachmentId, CancellationToken cancellationToken = default);
    void AddExecution(ExecutionEntity execution);
    void RemoveExecution(string executionId);
    void SaveTask(Services.TaskImplementation task);
    void DeleteTask(string taskId);
    Task<Services.TaskImplementation?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<AttachmentEntity>> FindAttachmentsAsync(Func<AttachmentEntity, bool>? predicate = null, CancellationToken cancellationToken = default);
}

public abstract class NeedsActiveTaskCmd<T> : ICommand<T>
{
    protected string TaskId { get; }

    protected NeedsActiveTaskCmd(string taskId)
    {
        TaskId = taskId;
    }

    public virtual T Execute(ICommandContext context) => throw new NotSupportedException("This command is async-only. Use ExecuteAsync.");

    public async Task<T> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(TaskId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("taskId is null");

        var task = await ResolveTaskAsync(context, cancellationToken);
        if (task == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineObjectNotFoundException(
                $"Cannot find task with id '{TaskId}'", typeof(Services.TaskImplementation));

        ValidateTaskState(task);
        return await ExecuteAsync(context, task, cancellationToken);
    }

    protected abstract T Execute(ICommandContext context, Services.TaskImplementation task);
    protected virtual Task<T> ExecuteAsync(ICommandContext context, Services.TaskImplementation task, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This command is async-only. Override ExecuteAsync.");

    protected virtual async Task<Services.TaskImplementation?> ResolveTaskAsync(
        ICommandContext context,
        CancellationToken cancellationToken)
    {
        var execution = await context.FindExecutionByTaskIdAsync(TaskId, cancellationToken);
        if (execution != null)
        {
            var task = execution.TaskEntities.FirstOrDefault(t => t.Id == TaskId);
            if (task != null)
                return task;
        }

        return await context.GetTaskAsync(TaskId, cancellationToken)
            ?? (await context.FindTasksAsync(t => t.Id == TaskId, cancellationToken)).FirstOrDefault();
    }

    protected virtual void ValidateTaskState(Services.TaskImplementation task)
    {
        if (task.DelegationState == "SUSPENDED")
            throw new AsterERP.Workflow.Common.WorkflowEngineException(GetSuspendedTaskException());
    }

    protected abstract string GetSuspendedTaskException();
}

public abstract class NeedsActiveExecutionCmd<T> : ICommand<T>
{
    protected string ExecutionId { get; }

    protected NeedsActiveExecutionCmd(string executionId)
    {
        ExecutionId = executionId;
    }

    public T Execute(ICommandContext context) => throw new NotSupportedException("This command is async-only. Use ExecuteAsync.");

    public async Task<T> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ExecutionId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("executionId is null");

        var execution = await context.GetCurrentExecutionAsync(ExecutionId, cancellationToken);
        ValidateExecutionState(execution!);
        return await ExecuteAsync(context, execution, cancellationToken);
    }

    protected abstract T Execute(ICommandContext context, ExecutionEntity execution);
    protected virtual Task<T> ExecuteAsync(ICommandContext context, ExecutionEntity execution, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This command is async-only. Override ExecuteAsync.");

    protected virtual void ValidateExecutionState(ExecutionEntity execution)
    {
        if (!execution.IsActive)
            throw new AsterERP.Workflow.Common.WorkflowEngineException(GetSuspendedExceptionMessage());
    }

    protected abstract string GetSuspendedExceptionMessage();
}

internal class FindExecutionByTaskIdCmd : ICommand<ExecutionEntity?>
{
    private readonly string _taskId;

    public FindExecutionByTaskIdCmd(string taskId)
    {
        _taskId = taskId;
    }

    public ExecutionEntity? Execute(ICommandContext context)
    {
        throw new NotSupportedException("This command is async-only. Use ExecuteAsync.");
    }

    public async Task<ExecutionEntity?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return await context.FindExecutionByTaskIdAsync(_taskId, cancellationToken);
    }
}

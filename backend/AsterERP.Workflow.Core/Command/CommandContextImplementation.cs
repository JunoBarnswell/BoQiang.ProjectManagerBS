using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Context;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Command;

public class CommandContextImplementation : ICommandContext
{
    private readonly IProcessEngineConfiguration _config;
    private readonly Dictionary<Type, ISession> _sessions = new();
    private readonly ICommandContextSession _session;
    private readonly ConcurrentDictionary<string, ExecutionEntity> _localExecutions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _rootExecutionIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _deletedProcessInstances = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CommentEntity> _comments = new(StringComparer.Ordinal);
    private readonly HashSet<string> _deletedComments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AttachmentEntity> _attachments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _attachmentContents = new(StringComparer.Ordinal);
    private readonly HashSet<string> _deletedAttachments = new(StringComparer.Ordinal);
    private readonly List<ICommandContextCloseListener> _closeListeners = new();
    private readonly ConcurrentDictionary<string, TaskImplementation> _localTasks = new(StringComparer.Ordinal);
    private readonly HashSet<string> _deletedTasks = new(StringComparer.Ordinal);
    private readonly bool _ownsTransaction;
    private bool _closed;
    private bool _initialized;

    public CommandContextImplementation(IProcessEngineConfiguration config)
    {
        _config = config;
        _session = GetSession<ICommandContextSession>();
        _ownsTransaction = !_session.HasActiveTransaction;
    }

    public IProcessEngineConfiguration ProcessEngineConfiguration => _config;
    public bool IsClosed => _closed;

    public TSession GetSession<TSession>() where TSession : class, ISession
    {
        EnsureNotClosed();

        var sessionType = typeof(TSession);
        if (_sessions.TryGetValue(sessionType, out var existingSession))
        {
            return (TSession)existingSession;
        }

        var sessionFactory = ResolveSessionFactory(sessionType);
        var session = sessionFactory.OpenSession(this);
        _sessions[sessionType] = session;
        return (TSession)session;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_initialized)
        {
            return;
        }

        if (_ownsTransaction)
        {
            await _session.BeginAsync(cancellationToken);
        }

        _initialized = true;
    }

    public async Task<ExecutionEntity?> GetCurrentExecutionAsync(string executionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_closed)
        {
            return null;
        }

        if (_localExecutions.TryGetValue(executionId, out var execution))
        {
            return execution;
        }

        var processInstanceId = await _session.FindProcessInstanceIdByExecutionIdAsync(executionId, cancellationToken);
        if (processInstanceId == null)
        {
            return null;
        }

        var root = await _session.LoadExecutionTreeAsync(processInstanceId, cancellationToken);
        if (root == null)
        {
            return null;
        }

        CacheExecutionTree(root, registerRoot: true);
        return _localExecutions.TryGetValue(executionId, out execution) ? execution : null;
    }

    public async Task<ExecutionEntity?> FindExecutionByTaskIdAsync(string taskId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_closed)
        {
            return null;
        }

        foreach (var execution in _localExecutions.Values)
        {
            var found = FindExecutionByTaskId(execution, taskId);
            if (found != null)
            {
                return found;
            }
        }

        var task = await _session.GetTaskAsync(taskId, cancellationToken);
        if (task?.ProcessInstanceId == null)
        {
            return null;
        }

        var root = await _session.LoadExecutionTreeAsync(task.ProcessInstanceId, cancellationToken);
        if (root == null)
        {
            return null;
        }

        CacheExecutionTree(root, registerRoot: true);
        foreach (var execution in _localExecutions.Values)
        {
            var found = FindExecutionByTaskId(execution, taskId);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    public async Task<IReadOnlyCollection<TaskImplementation>> FindTasksAsync(
        Func<TaskImplementation, bool>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var taskMap = new Dictionary<string, TaskImplementation>(StringComparer.Ordinal);

        foreach (var task in await _session.GetTasksAsync(cancellationToken))
        {
            taskMap[task.Id] = task;
        }

        foreach (var execution in _localExecutions.Values)
        {
            foreach (var task in execution.TaskEntities)
            {
                taskMap[task.Id] = task;
            }
        }

        foreach (var task in _localTasks.Values)
        {
            taskMap[task.Id] = task;
        }

        foreach (var taskId in _deletedTasks)
        {
            taskMap.Remove(taskId);
        }

        return predicate == null
            ? taskMap.Values.ToList()
            : taskMap.Values.Where(predicate).ToList();
    }

    public async Task<IReadOnlyCollection<ExecutionEntity>> FindExecutionsAsync(
        Func<ExecutionEntity, bool>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (predicate == null)
        {
            return await BuildExecutionSnapshotWithoutTreeLoadAsync(cancellationToken);
        }

        var records = await _session.GetExecutionsAsync(cancellationToken);
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var processInstanceId = record.ProcessInstanceId ?? record.Id;
            if (!_deletedProcessInstances.Contains(processInstanceId) &&
                !_rootExecutionIds.Contains(processInstanceId))
            {
                var root = await _session.LoadExecutionTreeAsync(processInstanceId, cancellationToken);
                if (root != null)
                {
                    CacheExecutionTree(root, registerRoot: true);
                }
            }
        }

        var executions = new List<ExecutionEntity>();
        var visitedExecutions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rootExecutionId in _rootExecutionIds)
        {
            if (_localExecutions.TryGetValue(rootExecutionId, out var root))
            {
                CollectExecutions(root, executions, visitedExecutions);
            }
        }

        return executions.Where(predicate).ToList();
    }

    public void AddExecution(ExecutionEntity execution)
    {
        ArgumentNullException.ThrowIfNull(execution);
        CacheExecutionTree(execution, registerRoot: true);
        _deletedProcessInstances.Remove(execution.ProcessInstanceId ?? execution.Id);
    }

    public void AddComment(CommentEntity comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        _comments[comment.Id] = comment;
        _deletedComments.Remove(comment.Id);
    }

    public void DeleteComment(string commentId)
    {
        _comments.Remove(commentId);
        _deletedComments.Add(commentId);
    }

    public async Task<IReadOnlyCollection<CommentEntity>> FindCommentsAsync(
        Func<CommentEntity, bool>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var comments = await _session.GetCommentsAsync(cancellationToken);
        var commentMap = comments
            .Where(comment => !_deletedComments.Contains(comment.Id))
            .ToDictionary(comment => comment.Id, StringComparer.Ordinal);

        foreach (var comment in _comments.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            commentMap[comment.Id] = comment;
        }

        foreach (var commentId in _deletedComments)
        {
            commentMap.Remove(commentId);
        }

        return predicate == null
            ? commentMap.Values.ToList()
            : commentMap.Values.Where(predicate).ToList();
    }

    public void SaveAttachment(AttachmentEntity attachment, byte[]? content = null)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        if (content != null && string.IsNullOrEmpty(attachment.ContentId))
        {
            attachment.ContentId = attachment.Id;
        }

        _attachments[attachment.Id] = attachment;
        _deletedAttachments.Remove(attachment.Id);
        if (content != null)
        {
            _attachmentContents[attachment.Id] = content.ToArray();
        }
    }

    public void DeleteAttachment(string attachmentId)
    {
        _attachments.Remove(attachmentId);
        _attachmentContents.Remove(attachmentId);
        _deletedAttachments.Add(attachmentId);
    }

    public async Task<IReadOnlyCollection<AttachmentEntity>> FindAttachmentsAsync(
        Func<AttachmentEntity, bool>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var attachments = await _session.GetAttachmentsAsync(cancellationToken);
        var attachmentMap = attachments
            .Where(attachment => !_deletedAttachments.Contains(attachment.Id))
            .ToDictionary(attachment => attachment.Id, StringComparer.Ordinal);

        foreach (var attachment in _attachments.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attachmentMap[attachment.Id] = attachment;
        }

        foreach (var attachmentId in _deletedAttachments)
        {
            attachmentMap.Remove(attachmentId);
        }

        return predicate == null
            ? attachmentMap.Values.ToList()
            : attachmentMap.Values.Where(predicate).ToList();
    }

    public async Task<byte[]?> GetAttachmentContentAsync(string attachmentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_deletedAttachments.Contains(attachmentId))
        {
            return null;
        }

        return _attachmentContents.TryGetValue(attachmentId, out var content)
            ? content.ToArray()
            : await _session.GetAttachmentContentAsync(attachmentId, cancellationToken);
    }

    public void SaveTask(TaskImplementation task)
    {
        ArgumentNullException.ThrowIfNull(task);
        _localTasks[task.Id] = task;
        _deletedTasks.Remove(task.Id);
    }

    public void DeleteTask(string taskId)
    {
        _localTasks.TryRemove(taskId, out _);
        _deletedTasks.Add(taskId);
    }

    public async Task<TaskImplementation?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_closed || _deletedTasks.Contains(taskId))
        {
            return null;
        }

        var loadedTask = FindTaskInLoadedExecutions(taskId);
        if (loadedTask != null)
        {
            return loadedTask;
        }

        if (_localTasks.TryGetValue(taskId, out var cachedTask))
        {
            return cachedTask;
        }

        var dbTask = await _session.GetTaskAsync(taskId, cancellationToken);
        if (dbTask != null)
        {
            _localTasks[taskId] = dbTask;
        }

        return dbTask;
    }

    public void RemoveExecution(string executionId)
    {
        if (_localExecutions.TryRemove(executionId, out var execution))
        {
            var processInstanceId = execution.ProcessInstanceId ?? execution.Id;
            if (execution.IsProcessInstanceType || execution.ParentId == null || execution.Id == processInstanceId)
            {
                _deletedProcessInstances.Add(processInstanceId);
                _rootExecutionIds.Remove(execution.Id);
            }

            foreach (var task in execution.TaskEntities)
            {
                DeleteTask(task.Id);
            }

            foreach (var child in execution.ChildExecutions.ToList())
            {
                RemoveExecution(child.Id);
            }
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotClosed();
        cancellationToken.ThrowIfCancellationRequested();
        await InitializeAsync(cancellationToken);

        try
        {
            NotifyCloseListeners(listener => listener.Closing(this));
            await _session.FlushAsync(BuildFlushState(), cancellationToken);
            NotifyCloseListeners(listener => listener.AfterSessionsFlush(this));
            if (_ownsTransaction)
            {
                await _session.CommitAsync(cancellationToken);
            }
            CloseSessions();
            NotifyCloseListeners(listener => listener.Closed(this));
        }
        catch
        {
            await RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            _closed = true;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotClosed();

        try
        {
            if (_ownsTransaction)
            {
                await _session.RollbackAsync(cancellationToken);
            }
            CloseSessions();
            NotifyCloseListeners(listener => listener.CloseFailure(this));
        }
        finally
        {
            _closed = true;
        }
    }

    public void AddCloseListener(ICommandContextCloseListener closeListener)
    {
        EnsureNotClosed();
        ArgumentNullException.ThrowIfNull(closeListener);
        _closeListeners.Add(closeListener);
    }

    public IReadOnlyCollection<ICommandContextCloseListener> GetCloseListeners()
    {
        return _closeListeners.ToList();
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_closed)
        {
            try
            {
                await RollbackAsync(cancellationToken);
            }
            finally
            {
                _closed = true;
            }
        }

        ClearState();
        GC.SuppressFinalize(this);
    }

    private CommandContextFlushState BuildFlushState()
    {
        return new CommandContextFlushState
        {
            Comments = _comments.Values.ToList(),
            DeletedCommentIds = _deletedComments.ToList(),
            Attachments = _attachments.Values.ToList(),
            AttachmentContents = _attachmentContents.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal),
            DeletedAttachmentIds = _deletedAttachments.ToList(),
            RuntimePersistenceBatch = BuildRuntimePersistenceBatch()
        };
    }

    private RuntimePersistenceBatch BuildRuntimePersistenceBatch()
    {
        foreach (var rootExecutionId in _rootExecutionIds.ToList())
        {
            if (!_localExecutions.TryGetValue(rootExecutionId, out var root))
            {
                continue;
            }

            var processInstanceId = root.ProcessInstanceId ?? root.Id;
            if (!root.IsEnded || (!root.IsProcessInstanceType && root.ParentId != null && root.Id != processInstanceId))
            {
                continue;
            }

            _deletedProcessInstances.Add(processInstanceId);
            _rootExecutionIds.Remove(rootExecutionId);
            RemoveExecution(rootExecutionId);
        }

        var roots = _rootExecutionIds
            .Select(rootExecutionId => _localExecutions.TryGetValue(rootExecutionId, out var root) ? root : null)
            .Where(root => root != null)
            .Select(root => root!)
            .Where(root => !_deletedProcessInstances.Contains(root.ProcessInstanceId ?? root.Id))
            .ToList();
        var executionTaskIds = _localExecutions.Values
            .SelectMany(execution => execution.TaskEntities)
            .Select(task => task.Id)
            .ToHashSet(StringComparer.Ordinal);
        var standaloneTasks = _localTasks.Values
            .Where(task => !executionTaskIds.Contains(task.Id))
            .Where(task => !_deletedTasks.Contains(task.Id))
            .ToList();

        return new RuntimePersistenceBatch
        {
            RootExecutions = roots,
            StandaloneTasks = standaloneTasks,
            DeletedProcessInstanceIds = _deletedProcessInstances.ToList(),
            DeletedTaskIds = _deletedTasks.ToList()
        };
    }

    private async Task<IReadOnlyCollection<ExecutionEntity>> BuildExecutionSnapshotWithoutTreeLoadAsync(CancellationToken cancellationToken)
    {
        var executionMap = new Dictionary<string, ExecutionEntity>(StringComparer.Ordinal);

        foreach (var record in await _session.GetExecutionsAsync(cancellationToken))
        {
            executionMap[record.Id] = MapExecutionRecord(record);
        }

        foreach (var execution in _localExecutions.Values)
        {
            executionMap[execution.Id] = execution;
        }

        foreach (var processInstanceId in _deletedProcessInstances)
        {
            var idsToRemove = executionMap.Values
                .Where(item => string.Equals(item.ProcessInstanceId ?? item.Id, processInstanceId, StringComparison.Ordinal))
                .Select(item => item.Id)
                .ToList();
            foreach (var id in idsToRemove)
            {
                executionMap.Remove(id);
            }
        }

        return executionMap.Values.ToList();
    }

    private ISessionFactory ResolveSessionFactory(Type sessionType)
    {
        var sessionFactories = ProcessEngineServiceProviderAccessor.GetServices<ISessionFactory>(_config).ToList();
        if (sessionFactories.Count == 0)
        {
            throw new InvalidOperationException("ISessionFactory is required for the runtime command chain.");
        }

        var sessionFactory = sessionFactories.FirstOrDefault(factory => sessionType.IsAssignableFrom(factory.SessionType));
        if (sessionFactory == null)
        {
            throw new InvalidOperationException($"No session factory registered for session type '{sessionType.Name}'.");
        }

        return sessionFactory;
    }

    private void CloseSessions()
    {
        foreach (var session in _sessions.Values)
        {
            session.Close();
        }
    }

    private void CacheExecutionTree(ExecutionEntity execution, bool registerRoot)
    {
        _localExecutions[execution.Id] = execution;
        if (registerRoot)
        {
            _rootExecutionIds.Add(execution.Id);
        }

        foreach (var child in execution.ChildExecutions)
        {
            CacheExecutionTree(child, registerRoot: false);
        }
    }

    private TaskImplementation? FindTaskInLoadedExecutions(string taskId)
    {
        foreach (var execution in _localExecutions.Values)
        {
            var task = execution.TaskEntities.FirstOrDefault(existingTask => existingTask.Id == taskId);
            if (task != null)
            {
                return task;
            }
        }

        return null;
    }

    private void EnsureNotClosed()
    {
        if (_closed)
        {
            throw new InvalidOperationException("CommandContext already closed.");
        }
    }

    private void NotifyCloseListeners(Action<ICommandContextCloseListener> action)
    {
        foreach (var listener in _closeListeners)
        {
            action(listener);
        }
    }

    private void ClearState()
    {
        _comments.Clear();
        _deletedComments.Clear();
        _attachments.Clear();
        _attachmentContents.Clear();
        _deletedAttachments.Clear();
        _closeListeners.Clear();
        _localExecutions.Clear();
        _rootExecutionIds.Clear();
        _deletedProcessInstances.Clear();
        _localTasks.Clear();
        _deletedTasks.Clear();
        _sessions.Clear();
    }

    private static ExecutionEntity? FindExecutionByTaskId(ExecutionEntity execution, string taskId)
    {
        if (execution.TaskEntities.Exists(task => task.Id == taskId))
        {
            return execution;
        }

        foreach (var child in execution.ChildExecutions)
        {
            var found = FindExecutionByTaskId(child, taskId);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void CollectExecutions(
        ExecutionEntity execution,
        ICollection<ExecutionEntity> executions,
        ISet<string> visitedExecutions)
    {
        if (!visitedExecutions.Add(execution.Id))
        {
            return;
        }

        executions.Add(execution);
        foreach (var child in execution.ChildExecutions)
        {
            CollectExecutions(child, executions, visitedExecutions);
        }
    }

    private static ExecutionEntity MapExecutionRecord(ExecutionRecord record)
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
}

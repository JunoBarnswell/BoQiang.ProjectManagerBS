using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Services;
using Xunit;

namespace AsterERP.Api.Tests.Workflow.Core.Cmd;

public sealed class AsyncOnlyCommandContextTests
{
    [Fact]
    public async Task GetDataObjectCmd_ExecuteAsync_ReturnsExecutionVariable()
    {
        var execution = CreateExecution("execution-1", ("amount", 42m));
        var context = new FakeCommandContext(execution);
        var command = new GetDataObjectCmd("execution-1", "amount");

        var result = await command.ExecuteAsync(context);

        Assert.NotNull(result);
        Assert.Equal("amount", result!.Name);
        Assert.Equal(42m, result.Value);
    }

    [Fact]
    public async Task GetDataObjectsCmd_ExecuteAsync_ReturnsRequestedExecutionVariables()
    {
        var execution = CreateExecution("execution-1", ("amount", 42m), ("customer", "Acme"));
        var context = new FakeCommandContext(execution);
        var command = new GetDataObjectsCmd("execution-1", new[] { "amount", "customer" });

        var result = await command.ExecuteAsync(context);

        Assert.Equal(2, result.Count);
        Assert.Equal(42m, result["amount"].Value);
        Assert.Equal("Acme", result["customer"].Value);
    }

    [Fact]
    public async Task AbstractSetProcessInstanceStateCmd_ExecuteAsync_UpdatesProcessInstanceAndChildren()
    {
        var execution = CreateProcessInstance("process-instance-1", isActive: false);
        var context = new FakeCommandContext(execution);
        var command = new TestSetProcessInstanceStateCmd("process-instance-1");

        var result = await command.ExecuteAsync(context);

        Assert.Null(result);
        Assert.True(execution.IsActive);
        Assert.All(execution.ChildExecutions, child => Assert.True(child.IsActive));
    }

    private static ExecutionEntity CreateProcessInstance(string executionId, bool isActive)
    {
        var childOne = new ExecutionEntity { Id = $"{executionId}-child-1", IsActive = !isActive };
        var childTwo = new ExecutionEntity { Id = $"{executionId}-child-2", IsActive = !isActive };

        return new ExecutionEntity
        {
            Id = executionId,
            IsProcessInstanceType = true,
            IsActive = isActive,
            ChildExecutions = new List<ExecutionEntity> { childOne, childTwo }
        };
    }

    private static ExecutionEntity CreateExecution(string executionId, params (string Name, object? Value)[] variables)
    {
        var execution = new ExecutionEntity
        {
            Id = executionId,
            IsProcessInstanceType = true
        };

        foreach (var variable in variables)
        {
            execution.Variables[variable.Name] = variable.Value;
        }

        return execution;
    }

    private sealed class TestSetProcessInstanceStateCmd : AbstractSetProcessInstanceStateCmd
    {
        public TestSetProcessInstanceStateCmd(string processInstanceId) : base(processInstanceId)
        {
        }

        protected override SuspensionState GetNewState() => SuspensionState.Active;
    }

    private sealed class FakeCommandContext : ICommandContext
    {
        private readonly Dictionary<string, ExecutionEntity> _executions;

        public FakeCommandContext(params ExecutionEntity[] executions)
        {
            _executions = executions.ToDictionary(execution => execution.Id, execution => execution, StringComparer.Ordinal);
            ProcessEngineConfiguration = new ProcessEngineConfiguration
            {
                EventDispatcher = new EventDispatcherImplementation()
            };
        }

        public IProcessEngineConfiguration ProcessEngineConfiguration { get; }

        public Task<ExecutionEntity?> GetCurrentExecutionAsync(string executionId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _executions.TryGetValue(executionId, out var execution);
            return Task.FromResult<ExecutionEntity?>(execution);
        }

        public TSession GetSession<TSession>() where TSession : class, ISession => throw new NotSupportedException();

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void AddCloseListener(ICommandContextCloseListener closeListener) => throw new NotSupportedException();

        public IReadOnlyCollection<ICommandContextCloseListener> GetCloseListeners() => Array.Empty<ICommandContextCloseListener>();

        public bool IsClosed => false;

        public Task<ExecutionEntity?> FindExecutionByTaskIdAsync(string taskId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<ExecutionEntity>> FindExecutionsAsync(Func<ExecutionEntity, bool>? predicate = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<CommentEntity>> FindCommentsAsync(Func<CommentEntity, bool>? predicate = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<TaskImplementation>> FindTasksAsync(Func<TaskImplementation, bool>? predicate = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void AddComment(CommentEntity comment) => throw new NotSupportedException();

        public void DeleteComment(string commentId) => throw new NotSupportedException();

        public void SaveAttachment(AttachmentEntity attachment, byte[]? content = null) => throw new NotSupportedException();

        public void DeleteAttachment(string attachmentId) => throw new NotSupportedException();

        public Task<byte[]?> GetAttachmentContentAsync(string attachmentId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void AddExecution(ExecutionEntity execution) => throw new NotSupportedException();

        public void RemoveExecution(string executionId) => throw new NotSupportedException();

        public void SaveTask(TaskImplementation task) => throw new NotSupportedException();

        public void DeleteTask(string taskId) => throw new NotSupportedException();

        public Task<TaskImplementation?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<AttachmentEntity>> FindAttachmentsAsync(Func<AttachmentEntity, bool>? predicate = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

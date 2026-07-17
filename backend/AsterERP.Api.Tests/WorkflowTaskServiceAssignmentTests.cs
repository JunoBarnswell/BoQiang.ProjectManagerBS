using System.Reflection;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Service;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowTaskServiceAssignmentTests
{
    [Fact]
    public async Task SetAssigneeAsync_ShouldCreateUserAssignmentWithoutGroupId()
    {
        var executor = new CapturingCommandExecutor();
        var service = new TaskServiceImplementation(executor);

        await service.SetAssigneeAsync("task-1", "wf_delegate");

        var command = Assert.IsType<AddIdentityLinkCmd>(executor.LastCommand);
        Assert.Equal("wf_delegate", ReadPrivateField<string?>(command, "_userId"));
        Assert.Null(ReadPrivateField<string?>(command, "_groupId"));
        Assert.Equal(IdentityLinkType.ASSIGNEE, ReadPrivateField<string>(command, "_type"));
    }

    [Fact]
    public async Task SetOwnerAsync_ShouldCreateUserAssignmentWithoutGroupId()
    {
        var executor = new CapturingCommandExecutor();
        var service = new TaskServiceImplementation(executor);

        await service.SetOwnerAsync("task-1", "wf_manager_approver");

        var command = Assert.IsType<AddIdentityLinkCmd>(executor.LastCommand);
        Assert.Equal("wf_manager_approver", ReadPrivateField<string?>(command, "_userId"));
        Assert.Null(ReadPrivateField<string?>(command, "_groupId"));
        Assert.Equal(IdentityLinkType.OWNER, ReadPrivateField<string>(command, "_type"));
    }

    private static T ReadPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        return (T)field.GetValue(instance)!;
    }

    private sealed class CapturingCommandExecutor : ICommandExecutor
    {
        public object? LastCommand { get; private set; }

        public ICommandInterceptor First { get; } = new CapturingCommandInterceptor();

        public T Execute<T>(ICommand<T> command)
        {
            LastCommand = command;
            return default!;
        }

        public Task<T> ExecuteAsync<T>(ICommand<T> command, CancellationToken cancellationToken = default)
        {
            LastCommand = command;
            return Task.FromResult(default(T)!);
        }
    }

    private sealed class CapturingCommandInterceptor : ICommandInterceptor
    {
        public ICommandInterceptor? Next { get; set; }

        public T Execute<T>(ICommand<T> command, Func<ICommand<T>, T> next) => next(command);

        public Task<T> ExecuteAsync<T>(
            ICommand<T> command,
            Func<ICommand<T>, Task<T>> next,
            CancellationToken cancellationToken = default) => next(command);
    }
}

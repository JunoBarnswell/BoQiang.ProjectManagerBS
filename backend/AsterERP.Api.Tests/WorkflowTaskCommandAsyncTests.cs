using AsterERP.Workflow.Core.Cmd;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowTaskCommandAsyncTests
{
    [Fact]
    public void TaskAndProcessCommands_RejectSynchronousExecution()
    {
        var commands = new Action[]
        {
            () => new HasTaskVariableCmd("task-1", "status", false).Execute(null!),
            () => new GetTaskVariableCmd("task-1", "status", false).Execute(null!),
            () => new GetTaskVariableInstanceCmd("task-1", "status", false).Execute(null!),
            () => new GetTaskVariableInstancesCmd("task-1", null, false).Execute(null!),
            () => new GetTasksLocalVariablesCmd(new[] { "task-1" }).Execute(null!),
            () => new GetTaskEventsCmd("task-1").Execute(null!),
            () => new GetTaskEventCmd("event-1").Execute(null!),
            () => new GetTaskCommentsCmd("task-1").Execute(null!),
            () => new GetTaskCommentsByTypeCmd("task-1", "comment").Execute(null!),
            () => new GetTypeCommentsCmd("comment").Execute(null!),
            () => new GetTaskAttachmentsCmd("task-1").Execute(null!),
            () => new GetTaskDataObjectCmd("task-1", "status").Execute(null!),
            () => new GetTaskDataObjectsCmd("task-1", null).Execute(null!),
            () => new GetSubTasksCmd("task-1").Execute(null!),
            () => new CompleteAdhocSubProcessCmd("execution-1").Execute(null!),
            () => new RemoveTaskVariablesCmd("task-1", new[] { "status" }).Execute(null!),
            () => new ActivateProcessInstanceCmd("process-1").Execute(null!),
            () => new DeleteProcessInstanceCmd("process-1").Execute(null!)
        };

        foreach (var command in commands)
        {
            var exception = Assert.Throws<NotSupportedException>(command);
            Assert.Contains("async-only", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AsyncCommandContractsRemainAvailable()
    {
        var commands = new object[]
        {
            new GetTaskVariableCmd("task-1", "status", false),
            new RemoveTaskVariablesCmd("task-1", new[] { "status" }),
            new ActivateProcessInstanceCmd("process-1"),
            new DeleteProcessInstanceCmd("process-1")
        };

        Assert.All(commands, command =>
        {
            var method = command.GetType().GetMethod("ExecuteAsync");
            Assert.NotNull(method);
            Assert.Equal(typeof(Task<>), method!.ReturnType.GetGenericTypeDefinition());
            Assert.Contains(
                method.GetParameters(),
                parameter => parameter.ParameterType == typeof(CancellationToken));
        });
    }
}

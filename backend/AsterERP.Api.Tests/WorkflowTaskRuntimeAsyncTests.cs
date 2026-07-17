using System.Reflection;
using AsterERP.Workflow.Api.Task.Payload;
using AsterERP.Workflow.Api.Task.Runtime;
using AsterERP.Workflow.Core.Services;
using Xunit;
using ApiTaskStatus = AsterERP.Workflow.Api.Task.Payload.TaskStatus;

namespace AsterERP.Api.Tests;

public sealed class WorkflowTaskRuntimeAsyncTests
{
    [Fact]
    public void TaskRuntimeContractsExposeOnlyAsyncOperations()
    {
        var runtimeMethods = typeof(ITaskRuntime).GetMethods();
        var adminMethods = typeof(ITaskAdminRuntime).GetMethods();

        Assert.All(runtimeMethods, method => AssertTaskReturnType(method));
        Assert.All(adminMethods, method => AssertTaskReturnType(method));
        Assert.DoesNotContain(runtimeMethods, method => method.Name == "get_Tasks");
    }

    [Fact]
    public async Task ClaimTaskAsyncDoesNotBlockAndPropagatesCancellationToken()
    {
        var service = DispatchProxy.Create<ITaskService, TaskServiceProbe>();
        var probe = (TaskServiceProbe)(object)service;
        var runtime = new TaskRuntimeImplementation(service);
        using var cancellationSource = new CancellationTokenSource();

        var claim = runtime.ClaimTaskAsync(
            new ClaimTaskPayload { TaskId = "task-1", Assignee = "user-1" },
            cancellationSource.Token);

        Assert.False(claim.IsCompleted);
        Assert.Equal(cancellationSource.Token, probe.CapturedCancellationToken);

        probe.ClaimCompletion.SetResult();
        probe.GetTaskCompletion.SetResult(new TaskImplementation
        {
            Id = "task-1",
            Name = "Approval",
            Assignee = "user-1"
        });

        var result = await claim;

        Assert.Equal("task-1", result.Id);
        Assert.Equal(ApiTaskStatus.Assigned, result.Status);
    }

    private static void AssertTaskReturnType(MethodInfo method)
    {
        Assert.True(
            method.ReturnType == typeof(global::System.Threading.Tasks.Task) ||
            (method.ReturnType.IsGenericType &&
             method.ReturnType.GetGenericTypeDefinition() == typeof(global::System.Threading.Tasks.Task<>)),
            $"{method.DeclaringType?.Name}.{method.Name} must return Task.");
    }

    public class TaskServiceProbe : DispatchProxy
    {
        public TaskCompletionSource ClaimCompletion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<TaskImplementation?> GetTaskCompletion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken CapturedCancellationToken { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null)
                throw new ArgumentNullException(nameof(targetMethod));

            if (args != null)
            {
                var cancellationToken = args.OfType<CancellationToken>().FirstOrDefault();
                if (cancellationToken != default)
                    CapturedCancellationToken = cancellationToken;
            }

            return targetMethod.Name switch
            {
                nameof(ITaskService.ClaimTaskAsync) => ClaimCompletion.Task,
                nameof(ITaskService.GetTaskAsync) => GetTaskCompletion.Task,
                _ => CreateCompletedReturn(targetMethod.ReturnType)
            };
        }

        private static object CreateCompletedReturn(Type returnType)
        {
            if (returnType == typeof(global::System.Threading.Tasks.Task))
                return global::System.Threading.Tasks.Task.CompletedTask;

            if (returnType.IsGenericType &&
                returnType.GetGenericTypeDefinition() == typeof(global::System.Threading.Tasks.Task<>))
            {
                var resultType = returnType.GetGenericArguments()[0];
                var result = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
                var completionSourceType = typeof(TaskCompletionSource<>).MakeGenericType(resultType);
                var completionSource = Activator.CreateInstance(completionSourceType)!;
                completionSourceType.GetMethod(nameof(TaskCompletionSource<object>.SetResult))!
                    .Invoke(completionSource, new[] { result });
                return completionSourceType.GetProperty(nameof(TaskCompletionSource<object>.Task))!.GetValue(completionSource)!;
            }

            throw new NotSupportedException($"Unsupported proxy return type: {returnType}");
        }
    }
}

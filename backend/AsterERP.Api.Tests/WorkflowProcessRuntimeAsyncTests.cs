using System.Reflection;
using AsterERP.Workflow.Api.Process.Payload;
using AsterERP.Workflow.Api.Process.Runtime;
using AsterERP.Workflow.Core.Services;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowProcessRuntimeAsyncTests
{
    [Fact]
    public void ProcessRuntimeContractsExposeOnlyAsyncOperations()
    {
        Assert.All(typeof(IProcessRuntime).GetMethods(), AssertTaskReturnType);
        Assert.All(typeof(IProcessAdminRuntime).GetMethods(), AssertTaskReturnType);

        Assert.DoesNotContain(typeof(IProcessRuntime).GetMethods(), method =>
            method.Name is "Deploy" or "Start" or "Suspend" or "Resume" or "Delete" or
                "GetProcessDefinition" or "GetProcessInstance" or "get_ProcessDefinitions" or
                "get_ProcessInstances");
        Assert.DoesNotContain(typeof(IProcessAdminRuntime).GetMethods(), method =>
            method.Name is "SuspendProcessInstanceById" or "ActivateProcessInstanceById" or
                "DeleteProcessInstance");
    }

    [Fact]
    public async Task StartAsyncDoesNotBlockAndPropagatesCancellationToken()
    {
        var repository = DispatchProxy.Create<IRepositoryService, RepositoryServiceProbe>();
        var runtimeService = DispatchProxy.Create<IRuntimeService, RuntimeServiceProbe>();
        var history = DispatchProxy.Create<IHistoryService, HistoryServiceProbe>();
        var runtimeProbe = (RuntimeServiceProbe)(object)runtimeService;
        var processRuntime = new ProcessRuntimeImplementation(repository, runtimeService, history);
        using var cancellationSource = new CancellationTokenSource();

        var start = processRuntime.StartAsync(
            new StartPayload
            {
                ProcessDefinitionKey = "invoice-approval",
                BusinessKey = "invoice-001"
            },
            cancellationSource.Token);

        Assert.False(start.IsCompleted);
        Assert.Equal(cancellationSource.Token, runtimeProbe.CapturedCancellationToken);

        runtimeProbe.StartCompletion.SetResult("process-001");
        Assert.False(start.IsCompleted);

        runtimeProbe.ExecutionCompletion.SetResult(new ExecutionRecord
        {
            Id = "execution-001",
            ProcessInstanceId = "process-001",
            ProcessDefinitionId = "definition-001",
            BusinessKey = "invoice-001",
            IsActive = true
        });

        var result = await start;

        Assert.Equal("process-001", result.Id);
        Assert.Equal("definition-001", result.ProcessDefinitionId);
        Assert.Equal(ProcessInstanceStatus.Running, result.Status);
        Assert.Equal(cancellationSource.Token, runtimeProbe.ExecutionCancellationToken);
    }

    private static void AssertTaskReturnType(MethodInfo method)
    {
        Assert.True(
            method.ReturnType == typeof(global::System.Threading.Tasks.Task) ||
            (method.ReturnType.IsGenericType &&
             method.ReturnType.GetGenericTypeDefinition() == typeof(global::System.Threading.Tasks.Task<>)),
            $"{method.DeclaringType?.Name}.{method.Name} must return Task.");
    }

    private class RuntimeServiceProbe : DispatchProxy
    {
        public TaskCompletionSource<string> StartCompletion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<ExecutionRecord?> ExecutionCompletion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken CapturedCancellationToken { get; private set; }
        public CancellationToken ExecutionCancellationToken { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null)
                throw new ArgumentNullException(nameof(targetMethod));

            var cancellationToken = args?.OfType<CancellationToken>().FirstOrDefault() ?? default;
            if (targetMethod.Name == nameof(IRuntimeService.GetExecutionAsync))
            {
                ExecutionCancellationToken = cancellationToken;
                return ExecutionCompletion.Task;
            }

            if (targetMethod.Name == nameof(IRuntimeService.StartProcessInstanceByKeyAsync))
            {
                CapturedCancellationToken = cancellationToken;
                return StartCompletion.Task;
            }

            return CreateDefaultReturn(targetMethod.ReturnType);
        }
    }

    private class RepositoryServiceProbe : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod == null
                ? throw new ArgumentNullException(nameof(targetMethod))
                : CreateDefaultReturn(targetMethod.ReturnType);
    }

    private class HistoryServiceProbe : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod == null
                ? throw new ArgumentNullException(nameof(targetMethod))
                : CreateDefaultReturn(targetMethod.ReturnType);
    }

    private static object CreateDefaultReturn(Type returnType)
    {
        if (returnType == typeof(global::System.Threading.Tasks.Task))
            return global::System.Threading.Tasks.Task.CompletedTask;

        if (returnType.IsGenericType &&
            returnType.GetGenericTypeDefinition() == typeof(global::System.Threading.Tasks.Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var sourceType = typeof(TaskCompletionSource<>).MakeGenericType(resultType);
            var source = Activator.CreateInstance(sourceType)!;
            var result = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
            sourceType.GetMethod(nameof(TaskCompletionSource<object>.SetResult))!
                .Invoke(source, [result]);
            return sourceType.GetProperty(nameof(TaskCompletionSource<object>.Task))!.GetValue(source)!;
        }

        throw new NotSupportedException($"Unsupported proxy return type: {returnType}");
    }
}

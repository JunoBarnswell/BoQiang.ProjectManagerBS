using System.Reflection;
using AsterERP.Workflow.Approval.Core.Caching;
using AsterERP.Workflow.Approval.Core.Services.Workflow;
using AsterERP.Workflow.Core.Deploy;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.BpmnModel;
using Xunit;
using BpmnModelType = AsterERP.Workflow.BpmnModel.BpmnModel;

namespace AsterERP.Api.Tests;

public sealed class BpmnModelServiceAsyncTests
{
    [Fact]
    public async Task GetBpmnModelByProcessDefIdAsync_DoesNotBlockRepositoryTask()
    {
        var repositoryProxy = DispatchProxy.Create<IRepositoryService, RepositoryServiceDispatchProxy>();
        var repository = (RepositoryServiceDispatchProxy)(object)repositoryProxy;
        var model = new BpmnModelType();
        var pendingResult = new TaskCompletionSource<BpmnModelType>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.BpmnModelResult = pendingResult.Task;

        var service = new BpmnModelService(
            new CustomDeploymentCache<ProcessDefinitionCacheEntry>(),
            repositoryProxy);

        var operation = service.GetBpmnModelByProcessDefIdAsync("process-definition-1");

        Assert.False(operation.IsCompleted);
        pendingResult.SetResult(model);
        Assert.Same(model, await operation);
    }

    private class RepositoryServiceDispatchProxy : DispatchProxy
    {
        public Task<BpmnModelType> BpmnModelResult { get; set; } = Task.FromResult(new BpmnModelType());

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IRepositoryService.GetBpmnModelAsync))
            {
                return BpmnModelResult;
            }

            throw new NotSupportedException($"Unexpected repository method: {targetMethod?.Name}");
        }
    }
}

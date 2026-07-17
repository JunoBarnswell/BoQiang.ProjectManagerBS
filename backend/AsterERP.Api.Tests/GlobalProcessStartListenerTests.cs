using AsterERP.Workflow.Approval.Api.Enums.Workflow.Runtime;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Core.Listeners.Global;
using AsterERP.Workflow.Approval.Core.Services.Workflow;
using AsterERP.Workflow.Core.Event;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.Guids;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class GlobalProcessStartListenerTests
{
    [Fact]
    public async Task OnEventAsync_PersistsBusinessKeyFromProcessStartedEvent()
    {
        var service = new CapturingExtendProcinstService();
        using var provider = new ServiceCollection()
            .AddSingleton<IExtendProcinstService>(service)
            .BuildServiceProvider();
        var listener = new GlobalProcessStartListener(
            NullLogger<GlobalProcessStartListener>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>(),
            new TestGuidGenerator());

        await listener.OnEventAsync(WorkflowEventBuilder.CreateProcessStartedEvent(
            "process-instance-1",
            "MES:wf_browser_e2e_142717:1",
            "order-1"));

        Assert.NotNull(service.Saved);
        Assert.Equal("order-1", service.Saved.BusinessKey);
        Assert.Equal("process-instance-1", service.Saved.ProcessInstanceId);
        Assert.Equal("MES:wf_browser_e2e_142717:1", service.Saved.ProcessDefinitionId);
        Assert.Equal("MES:wf_browser_e2e_142717:1", service.Saved.ProcessName);
        Assert.Equal("MES:wf_browser_e2e_142717:1", service.Saved.ModelKey);
        Assert.Equal(ProcessStatusEnum.SPZ.ToString(), service.Saved.ProcessStatus);
    }

    [Fact]
    public async Task OnEventAsync_PersistsProcessMetadataFromEventEntity()
    {
        var service = new CapturingExtendProcinstService();
        using var provider = new ServiceCollection()
            .AddSingleton<IExtendProcinstService>(service)
            .BuildServiceProvider();
        var listener = new GlobalProcessStartListener(
            NullLogger<GlobalProcessStartListener>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>(),
            new TestGuidGenerator());

        await listener.OnEventAsync(new WorkflowEntityEvent(
            WorkflowEventType.PROCESS_STARTED,
            new
            {
                BusinessKey = "order-2",
                ProcessName = "Browser E2E Approval",
                ModelKey = "wf_browser_e2e_142717"
            },
            processInstanceId: "process-instance-2",
            processDefinitionId: "MES:wf_browser_e2e_142717:1"));

        Assert.NotNull(service.Saved);
        Assert.Equal("order-2", service.Saved.BusinessKey);
        Assert.Equal("Browser E2E Approval", service.Saved.ProcessName);
        Assert.Equal("wf_browser_e2e_142717", service.Saved.ModelKey);
    }

    private sealed class CapturingExtendProcinstService : IExtendProcinstService
    {
        public ExtendProcinst? Saved { get; private set; }

        public Task DeleteExtendProcinstByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<ExtendProcinst?> FindExtendProcinstByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Saved?.ProcessInstanceId == processInstanceId ? Saved : null);
        }

        public Task SaveExtendProcinstAndHisAsync(ExtendProcinst extendProcinst, CancellationToken cancellationToken = default)
        {
            Saved = extendProcinst;
            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(ProcessStatusEnum processStatus, string processInstanceId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestGuidGenerator : IGuidGenerator
    {
        public Guid Create()
        {
            return Guid.Parse("11111111-1111-1111-1111-111111111111");
        }
    }
}

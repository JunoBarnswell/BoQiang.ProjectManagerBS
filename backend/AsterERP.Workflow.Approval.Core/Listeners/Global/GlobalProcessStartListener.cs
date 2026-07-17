using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Approval.Api.Enums.Workflow.Runtime;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Core.Services.Workflow;
using AsterERP.Workflow.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.Guids;

namespace AsterERP.Workflow.Approval.Core.Listeners.Global;

public class GlobalProcessStartListener : IWorkflowEventListener
{
    private readonly ILogger<GlobalProcessStartListener> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGuidGenerator _guidGenerator;

    public GlobalProcessStartListener(
        ILogger<GlobalProcessStartListener> logger,
        IServiceScopeFactory scopeFactory,
        IGuidGenerator guidGenerator)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _guidGenerator = guidGenerator;
    }

    public bool IsFailOnException => true;

    public void OnEvent(IWorkflowEvent @event)
    {
        _ = ExecuteLegacyAsync(@event);
    }

    public async global::System.Threading.Tasks.Task OnEventAsync(
        IWorkflowEvent @event,
        CancellationToken cancellationToken = default)
    {
        var processInstanceId = @event.ProcessInstanceId;
        if (string.IsNullOrWhiteSpace(processInstanceId))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var extendProcinstService = scope.ServiceProvider.GetRequiredService<IExtendProcinstService>();

        var extendProcinst = await FindExistingAsync(
            extendProcinstService,
            processInstanceId,
            cancellationToken);
        if (extendProcinst != null)
        {
            return;
        }

        var processDefinitionId = @event.ProcessDefinitionId?.Trim() ?? string.Empty;
        var businessKey = ResolveBusinessKey(@event);
        var processMetadata = await ResolveProcessMetadataAsync(
            scope.ServiceProvider,
            @event,
            processDefinitionId,
            cancellationToken);

        extendProcinst = new ExtendProcinst
        {
            BusinessKey = string.IsNullOrWhiteSpace(businessKey) ? processInstanceId : businessKey,
            ProcessInstanceId = processInstanceId,
            ModelKey = processMetadata.ModelKey,
            ProcessDefinitionId = processDefinitionId,
            ProcessName = processMetadata.ProcessName,
            Id = _guidGenerator.Create().ToString("N"),
            ProcessStatus = ProcessStatusEnum.SPZ.ToString(),
            CurrentUserCode = string.Empty,
            TenantId = string.Empty,
            UserInfo = string.Empty,
            FormData = string.Empty,
            Creator = string.Empty,
            Updator = string.Empty,
            Keyword = string.Empty,
            DelFlag = 1
        };
        await extendProcinstService.SaveExtendProcinstAndHisAsync(extendProcinst, cancellationToken);
    }

    private async global::System.Threading.Tasks.Task ExecuteLegacyAsync(IWorkflowEvent @event)
    {
        try
        {
            await OnEventAsync(@event);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process legacy workflow event {EventType}", @event.Type);
        }
    }

    private static global::System.Threading.Tasks.Task<ExtendProcinst?> FindExistingAsync(
        IExtendProcinstService service,
        string processInstanceId,
        CancellationToken cancellationToken)
    {
        return service.FindExtendProcinstByProcessInstanceIdAsync(processInstanceId, cancellationToken);
    }

    private async global::System.Threading.Tasks.Task<(string ProcessName, string ModelKey)> ResolveProcessMetadataAsync(
        IServiceProvider serviceProvider,
        IWorkflowEvent @event,
        string processDefinitionId,
        CancellationToken cancellationToken)
    {
        var repositoryService = serviceProvider.GetService<IRepositoryService>();
        if (repositoryService is not null && !string.IsNullOrWhiteSpace(processDefinitionId))
        {
            try
            {
                var definition = await repositoryService
                    .GetProcessDefinitionByIdAsync(processDefinitionId, cancellationToken);
                if (definition is not null)
                {
                    return (
                        FirstNotEmpty(definition.Name, ResolveStringProperty(@event, "ProcessName"), processDefinitionId),
                        FirstNotEmpty(definition.Key, ResolveStringProperty(@event, "ModelKey"), processDefinitionId));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to resolve process definition metadata for {ProcessDefinitionId}",
                    processDefinitionId);
            }
        }

        return (
            FirstNotEmpty(
                ResolveStringProperty(@event, "ProcessName"),
                ResolveStringProperty(@event, "ProcessDefinitionName"),
                ResolveStringProperty(@event, "FormName"),
                ResolveStringProperty(@event, "Name"),
                processDefinitionId),
            FirstNotEmpty(
                ResolveStringProperty(@event, "ModelKey"),
                ResolveStringProperty(@event, "ProcessDefinitionKey"),
                ResolveStringProperty(@event, "Key"),
                processDefinitionId));
    }

    private static string ResolveBusinessKey(IWorkflowEvent @event)
    {
        if (@event is not WorkflowEntityEvent entityEvent || entityEvent.Entity is null)
        {
            return string.Empty;
        }

        if (entityEvent.Entity is string businessKey)
        {
            return businessKey.Trim();
        }

        var businessKeyProperty = entityEvent.Entity.GetType().GetProperty("BusinessKey");
        var value = businessKeyProperty?.GetValue(entityEvent.Entity);
        return value?.ToString()?.Trim() ?? string.Empty;
    }

    private static string ResolveStringProperty(IWorkflowEvent @event, string propertyName)
    {
        if (@event is not WorkflowEntityEvent entityEvent || entityEvent.Entity is null)
        {
            return string.Empty;
        }

        var property = entityEvent.Entity.GetType().GetProperty(propertyName);
        return property?.GetValue(entityEvent.Entity)?.ToString()?.Trim() ?? string.Empty;
    }

    private static string FirstNotEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }
}

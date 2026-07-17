using AsterERP.Workflow.Api.Process.Payload;
using AsterERP.Workflow.Api.Shared;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Persistence;

namespace AsterERP.Workflow.Api.Process.Runtime;

public class ProcessRuntimeImplementation : IProcessRuntime
{
    private readonly IRepositoryService _repositoryService;
    private readonly IRuntimeService _runtimeService;
    private readonly IHistoryService _historyService;

    public ProcessRuntimeImplementation(
        IRepositoryService repositoryService,
        IRuntimeService runtimeService,
        IHistoryService historyService)
    {
        _repositoryService = repositoryService;
        _runtimeService = runtimeService;
        _historyService = historyService;
    }

    public async Task<ProcessDefinitionPayload> DeployAsync(
        DeployPayload payload,
        CancellationToken cancellationToken = default)
    {
        var resources = new Dictionary<string, byte[]>();
        if (payload.ResourceName != null && payload.ResourceContent != null)
            resources[payload.ResourceName] = payload.ResourceContent;

        var deploymentId = await _repositoryService.DeployAsync(
            null, null, payload.TenantId, resources, cancellationToken: cancellationToken);

        var definitions = await _repositoryService.GetProcessDefinitionsAsync(cancellationToken);
        var definition = definitions.LastOrDefault(d => d.DeploymentId == deploymentId)
                         ?? definitions.LastOrDefault();

        if (definition == null)
            throw new WorkflowApiException(500, "Failed to deploy process definition");

        return MapDefinition(definition);
    }

    public async Task<ProcessInstancePayload> StartAsync(
        StartPayload payload,
        CancellationToken cancellationToken = default)
    {
        string processInstanceId;
        ExecutionRecord? startedExecution = null;

        if (!string.IsNullOrEmpty(payload.ProcessDefinitionKey))
        {
            processInstanceId = await _runtimeService.StartProcessInstanceByKeyAsync(
                payload.ProcessDefinitionKey, payload.BusinessKey, payload.Variables, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(payload.ProcessDefinitionId))
        {
            processInstanceId = await _runtimeService.StartProcessInstanceByIdAsync(
                payload.ProcessDefinitionId, payload.BusinessKey, payload.Variables, cancellationToken);
        }
        else
        {
            throw new WorkflowApiException(400, "ProcessDefinitionKey or ProcessDefinitionId is required");
        }

        startedExecution = await _runtimeService.GetExecutionAsync(processInstanceId, cancellationToken);

        return new ProcessInstancePayload
        {
            Id = processInstanceId,
            ProcessDefinitionId = startedExecution?.ProcessDefinitionId
                                  ?? payload.ProcessDefinitionId
                                  ?? payload.ProcessDefinitionKey!,
            ProcessDefinitionKey = payload.ProcessDefinitionKey,
            BusinessKey = payload.BusinessKey,
            Name = payload.Name,
            StartTime = AbpTimeIdProvider.UtcNow,
            Status = ProcessInstanceStatus.Running
        };
    }

    public async Task<ProcessInstancePayload> SuspendAsync(
        SuspendPayload payload,
        CancellationToken cancellationToken = default)
    {
        await _runtimeService.SuspendProcessInstanceByIdAsync(payload.ProcessInstanceId, cancellationToken);

        return await GetRuntimeProcessInstanceAsync(payload.ProcessInstanceId, cancellationToken);
    }

    public async Task<ProcessInstancePayload> ResumeAsync(
        ResumePayload payload,
        CancellationToken cancellationToken = default)
    {
        await _runtimeService.ActivateProcessInstanceByIdAsync(payload.ProcessInstanceId, cancellationToken);

        return await GetRuntimeProcessInstanceAsync(payload.ProcessInstanceId, cancellationToken);
    }

    public async Task<ProcessInstancePayload> DeleteAsync(
        DeletePayload payload,
        CancellationToken cancellationToken = default)
    {
        await _runtimeService.DeleteProcessInstanceAsync(
            payload.ProcessInstanceId,
            payload.Reason,
            cancellationToken);

        return new ProcessInstancePayload
        {
            Id = payload.ProcessInstanceId,
            Status = ProcessInstanceStatus.Cancelled
        };
    }

    public async Task<ProcessDefinitionPayload> GetProcessDefinitionAsync(
        GetProcessDefinitionPayload payload,
        CancellationToken cancellationToken = default)
    {
        ProcessDefinitionRecord? record = null;

        if (!string.IsNullOrEmpty(payload.ProcessDefinitionId))
            record = await _repositoryService.GetProcessDefinitionByIdAsync(
                payload.ProcessDefinitionId,
                cancellationToken);

        if (record == null)
            throw new WorkflowNotFoundException(
                $"Process definition not found (id={payload.ProcessDefinitionId}, key={payload.ProcessDefinitionKey})");

        return MapDefinition(record);
    }

    public async Task<ProcessInstancePayload> GetProcessInstanceAsync(
        GetProcessInstancePayload payload,
        CancellationToken cancellationToken = default)
    {
        var runtimeInstance = await FindRuntimeProcessInstanceAsync(
            payload.ProcessInstanceId,
            cancellationToken);
        if (runtimeInstance != null)
            return runtimeInstance;

        var historic = await _historyService.GetHistoricProcessInstanceAsync(
            payload.ProcessInstanceId,
            cancellationToken);

        if (historic != null)
        {
            return new ProcessInstancePayload
            {
                Id = historic.Id,
                ProcessDefinitionId = historic.ProcessDefinitionId ?? "",
                BusinessKey = historic.BusinessKey,
                StartTime = historic.StartTime,
                CompletedTime = historic.EndTime,
                Status = historic.EndTime.HasValue ? ProcessInstanceStatus.Completed : ProcessInstanceStatus.Running
            };
        }

        throw new WorkflowNotFoundException($"Process instance '{payload.ProcessInstanceId}' not found");
    }

    public async Task<IReadOnlyCollection<ProcessDefinitionPayload>> GetProcessDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var definitions = await _repositoryService.GetProcessDefinitionsAsync(cancellationToken);
        return definitions.Select(MapDefinition).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyCollection<ProcessInstancePayload>> GetProcessInstancesAsync(
        CancellationToken cancellationToken = default)
    {
        var executions = await _runtimeService.GetExecutionsAsync(cancellationToken);
        return executions
            .Where(e => e.ProcessInstanceId != null)
            .GroupBy(e => e.ProcessInstanceId!)
            .Select(g => MapRuntimeProcessInstance(g.Key, g))
            .ToList()
            .AsReadOnly();
    }

    private async Task<ProcessInstancePayload> GetRuntimeProcessInstanceAsync(
        string processInstanceId,
        CancellationToken cancellationToken)
    {
        var instance = await FindRuntimeProcessInstanceAsync(processInstanceId, cancellationToken);
        if (instance == null)
            throw new WorkflowNotFoundException($"Process instance '{processInstanceId}' not found");

        return instance;
    }

    private async Task<ProcessInstancePayload?> FindRuntimeProcessInstanceAsync(
        string processInstanceId,
        CancellationToken cancellationToken)
    {
        var executions = await _runtimeService.GetExecutionsAsync(cancellationToken);
        var relatedExecutions = executions
            .Where(e => e.ProcessInstanceId == processInstanceId || e.Id == processInstanceId)
            .ToList();

        return relatedExecutions.Count == 0
            ? null
            : MapRuntimeProcessInstance(processInstanceId, relatedExecutions);
    }

    private static ProcessInstancePayload MapRuntimeProcessInstance(
        string processInstanceId,
        IEnumerable<ExecutionRecord> executions)
    {
        var executionList = executions.ToList();
        var root = executionList.FirstOrDefault(e => e.Id == processInstanceId) ?? executionList.First();

        return new ProcessInstancePayload
        {
            Id = processInstanceId,
            ProcessDefinitionId = root.ProcessDefinitionId ?? "",
            BusinessKey = root.BusinessKey,
            Status = GetRuntimeStatus(executionList)
        };
    }

    private static ProcessInstanceStatus GetRuntimeStatus(IReadOnlyCollection<ExecutionRecord> executions)
    {
        if (executions.Any(e => e.IsActive))
            return ProcessInstanceStatus.Running;

        return executions.All(e => e.IsEnded)
            ? ProcessInstanceStatus.Completed
            : ProcessInstanceStatus.Suspended;
    }

    private static ProcessDefinitionPayload MapDefinition(ProcessDefinitionRecord record) => new()
    {
        Id = record.Id,
        Key = record.Key,
        Name = record.Name,
        Description = record.Description,
        Version = record.Version,
        DeploymentId = record.DeploymentId,
        Category = record.Category,
        TenantId = record.TenantId
    };
}

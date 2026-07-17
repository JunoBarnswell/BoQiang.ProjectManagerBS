using System;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.DynamicBpmn;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Engine;

public class ProcessEngineImplementation : IProcessEngine
{
    private readonly IProcessEngineConfiguration _configuration;
    private bool _isClosed;

    public string Name { get; }
    public IProcessEngineConfiguration ProcessEngineConfiguration => _configuration;
    public IRepositoryService RepositoryService { get; }
    public IRuntimeService RuntimeService { get; }
    public ITaskService TaskService { get; }
    public IHistoryService HistoryService { get; }
    public IManagementService ManagementService { get; }
    public IDynamicBpmnService? DynamicBpmnService { get; }
    public ICommandExecutor CommandExecutor => _configuration.CommandExecutor;

    public ProcessEngineImplementation(
        string name,
        IProcessEngineConfiguration configuration,
        IRepositoryService repositoryService,
        IRuntimeService runtimeService,
        ITaskService taskService,
        IHistoryService historyService,
        IManagementService managementService)
    {
        Name = name;
        _configuration = configuration;
        RepositoryService = repositoryService;
        RuntimeService = runtimeService;
        TaskService = taskService;
        HistoryService = historyService;
        ManagementService = managementService;
    }

    public ProcessEngineImplementation(
        string name,
        IProcessEngineConfiguration configuration,
        IRepositoryService repositoryService,
        IRuntimeService runtimeService,
        ITaskService taskService,
        IHistoryService historyService,
        IManagementService managementService,
        IDynamicBpmnService? dynamicBpmnService)
    {
        Name = name;
        _configuration = configuration;
        RepositoryService = repositoryService;
        RuntimeService = runtimeService;
        TaskService = taskService;
        HistoryService = historyService;
        ManagementService = managementService;
        DynamicBpmnService = dynamicBpmnService;
    }

    public void Dispose()
    {
        Close();
    }

    public void Close()
    {
        if (!_isClosed)
        {
            _isClosed = true;
        }
    }
}

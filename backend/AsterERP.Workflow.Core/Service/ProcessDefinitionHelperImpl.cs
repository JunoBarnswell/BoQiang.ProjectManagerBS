using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Common;

namespace AsterERP.Workflow.Core.Service;

public class ProcessDefinitionHelperImpl : IProcessDefinitionHelper
{
    private readonly ICommandExecutor _commandExecutor;

    public ProcessDefinitionHelperImpl(ICommandExecutor commandExecutor)
    {
        _commandExecutor = commandExecutor;
    }

    public ProcessDefinitionHelperImpl(IProcessEngineConfiguration processEngineConfiguration)
    {
        _commandExecutor = processEngineConfiguration.CommandExecutor;
    }

    public BpmnModel.Process? GetProcessDefinitionProcessObject(string processDefinitionId)
    {
        if (string.IsNullOrWhiteSpace(processDefinitionId))
            throw new WorkflowEngineArgumentException("processDefinitionId is null");

        var bpmnModel = GetProcessDefinitionBpmnModel(processDefinitionId);

        foreach (var process in bpmnModel.Processes)
        {
            if (process.Id == processDefinitionId)
                return process;
        }

        throw new WorkflowEngineObjectNotFoundException($"No process definition process found for id '{processDefinitionId}'", typeof(BpmnModel.Process));
    }

    public BpmnModel.BpmnModel? GetProcessDefinitionBpmnModel(string processDefinitionId)
    {
        if (string.IsNullOrWhiteSpace(processDefinitionId))
            throw new WorkflowEngineArgumentException("processDefinitionId is null");

        var model = _commandExecutor.Execute(new GetBpmnModelCmd(processDefinitionId));
        if (model == null || model.Processes == null || model.Processes.Count == 0)
        {
            throw new WorkflowEngineObjectNotFoundException($"No BPMN model found for process definition id '{processDefinitionId}'", typeof(BpmnModel.BpmnModel));
        }

        return model;
    }

    public static BpmnModel.Process? ResolveProcess(IProcessEngineConfiguration configuration, string processDefinitionId)
    {
        var helper = new ProcessDefinitionHelperImpl(configuration);
        return helper.GetProcessDefinitionProcessObject(processDefinitionId);
    }

    public static BpmnModel.BpmnModel? ResolveBpmnModel(IProcessEngineConfiguration configuration, string processDefinitionId)
    {
        var helper = new ProcessDefinitionHelperImpl(configuration);
        return helper.GetProcessDefinitionBpmnModel(processDefinitionId);
    }
}


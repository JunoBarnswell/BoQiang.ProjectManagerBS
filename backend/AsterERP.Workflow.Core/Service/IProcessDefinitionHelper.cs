using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Common;

namespace AsterERP.Workflow.Core.Service;

public interface IProcessDefinitionHelper
{
    BpmnModel.Process? GetProcessDefinitionProcessObject(string processDefinitionId);
    BpmnModel.BpmnModel? GetProcessDefinitionBpmnModel(string processDefinitionId);
}


using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.DynamicBpmn;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Engine;

public interface IProcessEngine : System.IDisposable
{
    string Name { get; }
    IProcessEngineConfiguration ProcessEngineConfiguration { get; }
    IRepositoryService RepositoryService { get; }
    IRuntimeService RuntimeService { get; }
    ITaskService TaskService { get; }
    IHistoryService HistoryService { get; }
    IManagementService ManagementService { get; }
    IDynamicBpmnService? DynamicBpmnService { get; }
    ICommandExecutor CommandExecutor { get; }
    void Close();
}

using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Engine;

namespace AsterERP.Workflow.Core.Service;

public abstract class ServiceImpl
{
    protected IProcessEngineConfiguration ProcessEngineConfiguration { get; }
    protected internal ICommandExecutor CommandExecutor { get; set; }

    protected ServiceImpl() { }

    protected ServiceImpl(IProcessEngineConfiguration processEngineConfiguration)
    {
        ProcessEngineConfiguration = processEngineConfiguration;
        CommandExecutor = processEngineConfiguration.CommandExecutor;
    }

    protected ServiceImpl(ICommandExecutor commandExecutor)
    {
        CommandExecutor = commandExecutor;
    }

    public ICommandExecutor GetCommandExecutor() => CommandExecutor;

    public void SetCommandExecutor(ICommandExecutor commandExecutor)
    {
        CommandExecutor = commandExecutor;
    }
}

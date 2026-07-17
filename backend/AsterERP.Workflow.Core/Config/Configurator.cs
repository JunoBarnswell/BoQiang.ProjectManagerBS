using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Engine;

namespace AsterERP.Workflow.Core.Config;

public interface IProcessEngineConfigurator
{
    void ConfigureBefore(IProcessEngineConfiguration processEngineConfiguration);
    void ConfigureAfter(IProcessEngineConfiguration processEngineConfiguration);
    int Priority { get; }
}

public abstract class AbstractProcessEngineConfigurator : IProcessEngineConfigurator
{
    public const int DefaultConfiguratorPriority = 10000;

    public virtual int Priority => DefaultConfiguratorPriority;

    public virtual void ConfigureBefore(IProcessEngineConfiguration processEngineConfiguration) { }

    public virtual void ConfigureAfter(IProcessEngineConfiguration processEngineConfiguration) { }
}

public class MailServerInfo
{
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseSSL { get; set; }
    public bool UseTLS { get; set; }
    public string? DefaultFrom { get; set; }
}

public interface ICommandExecutorFactory
{
    ICommandExecutor CreateExecutor(IExecutorContext executorContext);
}

public class CommandExecutorContext
{
    private static ICommandExecutorFactory? _shellCommandExecutorFactory;

    public static void SetShellExecutorContextFactory(ICommandExecutorFactory factory)
    {
        _shellCommandExecutorFactory = factory;
    }

    public static ICommandExecutorFactory? GetShellCommandExecutorFactory()
    {
        return _shellCommandExecutorFactory;
    }
}

public interface IExecutorContext
{
}

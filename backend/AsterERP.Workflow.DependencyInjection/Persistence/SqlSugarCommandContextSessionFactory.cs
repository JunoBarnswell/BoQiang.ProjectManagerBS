using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Context;
using AsterERP.Workflow.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AsterERP.Workflow.DependencyInjection.Persistence;

public sealed class SqlSugarCommandContextSessionFactory : ISessionFactory
{
    private readonly IServiceProvider _serviceProvider;

    public SqlSugarCommandContextSessionFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Type SessionType => typeof(ICommandContextSession);

    public ISession OpenSession(ICommandContext commandContext)
    {
        var serviceProvider = ProcessEngineServiceProviderAccessor.Current ?? _serviceProvider;
        var store = serviceProvider.GetRequiredService<IWorkflowPersistenceStore>();
        return new SqlSugarCommandContextSession(store);
    }
}

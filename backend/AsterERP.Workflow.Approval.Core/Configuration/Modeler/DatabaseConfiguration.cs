using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AsterERP.Workflow.Approval.Core.Configuration.Modeler;

public static class DatabaseConfiguration
{
    private const string LiquibaseChangelogPrefix = "ACT_DE_";

    public static IServiceCollection AddAsterERPFlowDatabase(this IServiceCollection services)
    {
        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;

namespace AsterERP.Workflow.Approval.Core.Configuration;

public static class TransactionConfig
{
    public static IServiceCollection AddAsterERPFlowTransaction(this IServiceCollection services)
    {
        return services;
    }
}

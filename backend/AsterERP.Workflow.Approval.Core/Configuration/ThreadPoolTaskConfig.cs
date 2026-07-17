using Microsoft.Extensions.DependencyInjection;

namespace AsterERP.Workflow.Approval.Core.Configuration;

public static class ThreadPoolTaskConfig
{
    private const int CorePoolSize = 5;
    private const int MaxPoolSize = 50;
    private const int KeepAliveTime = 10;
    private const int QueueCapacity = 200;
    private const string ThreadNamePrefix = "Flow-pool-";

    public static IServiceCollection AddAsterERPFlowThreadPool(this IServiceCollection services)
    {
        services.Configure<ThreadPoolOptions>(options =>
        {
            options.CorePoolSize = CorePoolSize;
            options.MaxPoolSize = MaxPoolSize;
            options.KeepAliveTimeSeconds = KeepAliveTime;
            options.QueueCapacity = QueueCapacity;
            options.ThreadNamePrefix = ThreadNamePrefix;
        });
        return services;
    }
}

public class ThreadPoolOptions
{
    public int CorePoolSize { get; set; } = 5;
    public int MaxPoolSize { get; set; } = 50;
    public int KeepAliveTimeSeconds { get; set; } = 10;
    public int QueueCapacity { get; set; } = 200;
    public string ThreadNamePrefix { get; set; } = "Flow-pool-";
}

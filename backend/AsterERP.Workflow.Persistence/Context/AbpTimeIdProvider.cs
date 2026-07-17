using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Persistence;

public static class AbpTimeIdProvider
{
    private static readonly AsyncLocal<IServiceProvider?> CurrentProvider = new();

    public static void Configure(IServiceProvider? serviceProvider)
    {
        CurrentProvider.Value = serviceProvider;
    }

    public static DateTime Now => ResolveClock()?.Now ?? DateTime.UtcNow;

    public static DateTime UtcNow => ResolveClock()?.Now.ToUniversalTime() ?? DateTime.UtcNow;

    public static string NewGuid(string format = "N") => ResolveGuid().ToString(format);

    private static IClock? ResolveClock()
    {
        return CurrentProvider.Value?.GetService<IClock>();
    }

    private static Guid ResolveGuid()
    {
        return CurrentProvider.Value?.GetService<IGuidGenerator>()?.Create() ?? Guid.NewGuid();
    }
}


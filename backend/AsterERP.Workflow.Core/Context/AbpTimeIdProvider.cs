using System;
using Microsoft.Extensions.DependencyInjection;
using AsterERP.Workflow.Core.Context;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Core;

public static class AbpTimeIdProvider
{
    public static DateTime Now => ResolveClock()?.Now ?? DateTime.UtcNow;

    public static DateTime UtcNow => ResolveClock()?.Now.ToUniversalTime() ?? DateTime.UtcNow;

    public static string NewGuid(string format = "N")
    {
        return ResolveGuid().ToString(format);
    }

    private static IClock? ResolveClock()
    {
        return ProcessEngineServiceProviderAccessor.Current?.GetService<IClock>();
    }

    private static Guid ResolveGuid()
    {
        return ProcessEngineServiceProviderAccessor.Current?.GetService<IGuidGenerator>()?.Create() ?? System.Guid.NewGuid();
    }

}


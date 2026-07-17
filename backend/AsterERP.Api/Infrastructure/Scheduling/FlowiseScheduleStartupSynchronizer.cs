using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Api.Modules.Ai.Flowise;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Scheduling;

public sealed class FlowiseScheduleStartupSynchronizer(
    IServiceScopeFactory scopeFactory,
    ILogger<FlowiseScheduleStartupSynchronizer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
        var scheduler = scope.ServiceProvider.GetRequiredService<IFlowiseScheduleScheduler>();
        var records = await db.Queryable<FlowiseScheduleRecordEntity>()
            .ClearFilter()
            .Where(item => !item.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var record in records)
        {
            await scheduler.ApplyAsync(record, cancellationToken);
        }

        logger.LogInformation("Synchronized {FlowiseScheduleCount} Flowise schedules with Hangfire", records.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

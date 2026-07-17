using SqlSugar;
using System.Diagnostics;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

public sealed class AiCenterModuleSeeder
{
    public Task SeedAsync(IServiceProvider serviceProvider, ISqlSugarClient db, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(AiCenterModuleSeeder));
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("AI Center seed step {StepName} started", nameof(AiCenterPermissionSeeder));
        AiCenterPermissionSeeder.Seed(db);
        logger.LogInformation("AI Center seed step {StepName} completed in {ElapsedMilliseconds} ms", nameof(AiCenterPermissionSeeder), stopwatch.ElapsedMilliseconds);

        stopwatch.Restart();
        logger.LogInformation("AI Center seed step {StepName} started", nameof(AiCenterMenuSeeder));
        AiCenterMenuSeeder.Seed(db);
        logger.LogInformation("AI Center seed step {StepName} completed in {ElapsedMilliseconds} ms", nameof(AiCenterMenuSeeder), stopwatch.ElapsedMilliseconds);

        stopwatch.Restart();
        logger.LogInformation("AI Center seed step {StepName} started", nameof(AiCenterDefaultDataSeeder));
        AiCenterDefaultDataSeeder.Seed(db);
        logger.LogInformation("AI Center seed step {StepName} completed in {ElapsedMilliseconds} ms", nameof(AiCenterDefaultDataSeeder), stopwatch.ElapsedMilliseconds);
        return Task.CompletedTask;
    }
}

using AsterERP.Api.Infrastructure.Abp.DevelopmentSeed;
using AsterERP.Api.Infrastructure.Abp.AiCenter;
using AsterERP.Api.Infrastructure.Abp.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;
using AsterERP.Api.Infrastructure.Abp.AsterScene;
using AsterERP.Api.Infrastructure.Abp.Im;
using AsterERP.Api.Infrastructure.Abp.WorkflowApproval;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Database;

public sealed class DbSeedService(
    ISqlSugarClient db,
    IServiceProvider serviceProvider,
    WorkflowApprovalSeedService workflowApprovalSeedService,
    AsterSceneSeedService asterSceneSeedService,
    ApplicationDataCenterSeedService applicationDataCenterSeedService,
    ApplicationDevelopmentCenterSeedService applicationDevelopmentCenterSeedService,
    AiCenterModuleSeeder aiCenterModuleSeeder,
    ImSeedService imSeedService,
    ILogger<DbSeedService> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await workflowApprovalSeedService.SeedAsync(cancellationToken);
        await asterSceneSeedService.SeedAsync(cancellationToken);
        await applicationDataCenterSeedService.SeedAsync(cancellationToken);
        await applicationDevelopmentCenterSeedService.SeedAsync(cancellationToken);
        await aiCenterModuleSeeder.SeedAsync(serviceProvider, db, cancellationToken);
        await imSeedService.SeedAsync(cancellationToken);

        var developmentSeedService = serviceProvider.GetService<IDevelopmentSeedService>();
        if (developmentSeedService is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation("Database seed started for ABP module {ModuleKey}", "development.seed");
            await developmentSeedService.SeedAsync(cancellationToken);
            logger.LogInformation("Database seed completed for ABP module {ModuleKey}", "development.seed");
        }

        logger.LogInformation(
            "Database seed completed through the explicit ABP seed chain and {DevelopmentSeedCount} development seed service",
            developmentSeedService is null ? 0 : 1);
    }
}

using AsterERP.Api.Application.System.ScheduledJobs;
using AsterERP.Api.Infrastructure.Abp;
using AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;
using AsterERP.Api.Infrastructure;
using AsterERP.Api.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using SqlSugar;
using Volo.Abp;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAsterErpInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddSignalR();
await builder.AddApplicationAsync<AsterErpAbpHostModule>();

using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();

if (args.Contains("--application-designer-deployment-migration", StringComparer.OrdinalIgnoreCase))
{
    var designerMigrator = scope.ServiceProvider.GetRequiredService<ApplicationDevelopmentCenterSchemaMigrator>();
    var db = scope.ServiceProvider.GetRequiredService<ISqlSugarClient>();
    await designerMigrator.RunDeploymentMigrationAsync(db, CancellationToken.None);
}

var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
await initializer.InitializeAsync();

if (!builder.Configuration.GetValue<bool>("Scheduler:SkipMigrationSync"))
{
    var scheduledJobService = scope.ServiceProvider.GetRequiredService<IScheduledJobService>();
    await scheduledJobService.SynchronizeAllAsync(CancellationToken.None);
}

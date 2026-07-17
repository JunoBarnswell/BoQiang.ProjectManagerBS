using AsterERP.Api.Modules.Platform;
using AsterERP.Contracts.ApplicationConsole;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationConsole;

public sealed class ApplicationDatabaseBindingMigrationService(
    ISqlSugarClient mainDb,
    ApplicationDatabaseBindingResolver bindingResolver,
    ILogger<ApplicationDatabaseBindingMigrationService> logger)
{
    public async Task<ApplicationDatabaseBindingMigrationReport> MigrateAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantApps = await mainDb.Queryable<SystemTenantAppEntity>()
            .Where(item => !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var items = new List<ApplicationDatabaseBindingMigrationItem>(tenantApps.Count);
        var migrated = 0;
        var canonical = 0;
        var notConfigured = 0;
        var failed = 0;

        foreach (var tenantApp in tenantApps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolution = bindingResolver.ResolveStatus(
                tenantApp.ConfigJson,
                tenantApp.TenantId,
                tenantApp.AppCode);
            if (resolution.Status == ApplicationDatabaseBindingStatus.NotConfigured)
            {
                notConfigured++;
                continue;
            }

            if (resolution.Status == ApplicationDatabaseBindingStatus.Ready)
            {
                canonical++;
                continue;
            }

            if (resolution.Status != ApplicationDatabaseBindingStatus.MigrationRequired)
            {
                failed++;
                items.Add(new ApplicationDatabaseBindingMigrationItem(
                    tenantApp.TenantId,
                    tenantApp.AppCode,
                    resolution.Status,
                    resolution.Message));
                continue;
            }

            try
            {
                var now = DateTime.UtcNow;
                var migratedJson = bindingResolver.MigrateLegacy(
                    tenantApp.ConfigJson!,
                    tenantApp.TenantId,
                    tenantApp.AppCode,
                    "application-database-binding-migration",
                    now);
                await mainDb.Updateable<SystemTenantAppEntity>()
                    .SetColumns(item => new SystemTenantAppEntity
                    {
                        ConfigJson = migratedJson,
                        UpdatedBy = "application-database-binding-migration",
                        UpdatedTime = now
                    })
                    .Where(item => item.Id == tenantApp.Id && !item.IsDeleted)
                    .ExecuteCommandAsync(cancellationToken);
                migrated++;
                items.Add(new ApplicationDatabaseBindingMigrationItem(
                    tenantApp.TenantId,
                    tenantApp.AppCode,
                    "Migrated",
                    "旧绑定已转换为 applicationDatabase canonical 节点"));
            }
            catch (ValidationException ex)
            {
                failed++;
                items.Add(new ApplicationDatabaseBindingMigrationItem(
                    tenantApp.TenantId,
                    tenantApp.AppCode,
                    "MigrationFailed",
                    ex.Message));
            }
        }

        var report = new ApplicationDatabaseBindingMigrationReport(
            tenantApps.Count,
            migrated,
            canonical,
            notConfigured,
            failed,
            items);
        logger.LogInformation(
            "Application database binding migration scanned {Scanned}, migrated {Migrated}, canonical {Canonical}, not configured {NotConfigured}, failed {Failed}",
            report.Scanned,
            report.Migrated,
            report.AlreadyCanonical,
            report.NotConfigured,
            report.Failed);
        return report;
    }
}

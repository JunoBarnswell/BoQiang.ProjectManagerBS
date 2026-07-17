using AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter.Migrations;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Contracts.ApplicationDevelopmentCenter;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDevelopmentCenterAbpModuleTests
{
    [Fact]
    public void Application_development_center_is_an_abp_module_with_direct_services_and_migrators()
    {
        Assert.True(typeof(AbpModule).IsAssignableFrom(typeof(AsterErpApplicationDevelopmentCenterModule)));

        var services = new ServiceCollection();
        new AsterErpApplicationDevelopmentCenterModule()
            .ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ApplicationDevelopmentCenterService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ApplicationDevelopmentCenterSchemaMigrator));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ApplicationDevelopmentCenterSeedService));
    }

    [Fact]
    public async Task Schema_migrator_is_idempotent_and_creates_all_module_tables()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:application-development-center-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });

        var migrator = new ApplicationDevelopmentCenterSchemaMigrator(new ApplicationDesignerDocumentMigrationService(db));
        await migrator.MigrateAsync(db, CancellationToken.None);
        await migrator.MigrateAsync(db, CancellationToken.None);

        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'app_dev_versions'"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'app_dev_shared_resources'"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'index' AND name = 'idx_app_dev_pages_workspace'"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM pragma_table_info('app_dev_pages') WHERE name = 'ParentPageId'"));
    }

    [Fact]
    public void Application_development_center_registers_all_workspace_data_filters()
    {
        var registry = new DataPermissionFilterRegistry();

        AsterErpApplicationDevelopmentCenterModule.RegisterDataFilters(registry);

        Assert.Equal(13, registry.WorkspaceEntityTypes.Count);
        Assert.Contains(typeof(ApplicationDevelopmentVersionEntity), registry.WorkspaceEntityTypes);
        Assert.Contains(typeof(ApplicationDevelopmentModuleEntity), registry.WorkspaceEntityTypes);
        Assert.Contains(typeof(ApplicationDevelopmentPageEntity), registry.WorkspaceEntityTypes);
        Assert.Contains(typeof(ApplicationSharedResourceEntity), registry.WorkspaceEntityTypes);
        Assert.Contains(typeof(ApplicationDesignerDocumentEntity), registry.WorkspaceEntityTypes);
        Assert.Contains(typeof(ApplicationDesignerRevisionEntity), registry.WorkspaceEntityTypes);
        Assert.Contains(typeof(ApplicationDesignerMigrationEntity), registry.WorkspaceEntityTypes);
        Assert.Contains(typeof(ApplicationDesignerRuntimeArtifactEntity), registry.WorkspaceEntityTypes);
        Assert.Contains(typeof(ApplicationDesignerEditorSessionEntity), registry.WorkspaceEntityTypes);
        Assert.Contains(typeof(ApplicationDesignerPublishRecordEntity), registry.WorkspaceEntityTypes);
        Assert.Contains(typeof(ApplicationDesignerMigrationRunEntity), registry.WorkspaceEntityTypes);
        Assert.Contains(typeof(ApplicationDesignerMigrationWatermarkEntity), registry.WorkspaceEntityTypes);
    }

    [Fact]
    public void Development_center_requests_are_classified_as_workspace_requests()
    {
        var classifier = new DataPermissionRequestClassifier();

        Assert.True(classifier.IsApplicationDevelopmentCenterApi("/api/application-development-center/overview"));
        Assert.False(classifier.IsApplicationDevelopmentCenterApi("/api/system/users"));
    }

    [Fact]
    public void Published_read_models_expose_runtime_artifact_metadata_not_schema_version()
    {
        Assert.Null(typeof(ApplicationDevelopmentPageListItemDto).GetProperty("PublishedSchemaVersionNo"));
        Assert.Null(typeof(ApplicationDevelopmentPageDetailDto).GetProperty("PublishedSchemaVersionNo"));
        Assert.Null(typeof(ApplicationDevelopmentPublishResponse).GetProperty("PublishedSchemaVersionNo"));
        Assert.NotNull(typeof(ApplicationDevelopmentPageDetailDto).GetProperty("PublishedArtifactHash"));
        Assert.NotNull(typeof(ApplicationDevelopmentPageDetailDto).GetProperty("PublishedArtifactRevision"));
        Assert.NotNull(typeof(ApplicationDevelopmentPageDetailDto).GetProperty("PublishedManifestHash"));
        Assert.NotNull(typeof(ApplicationDevelopmentPublishResponse).GetProperty("PublishedArtifactRevision"));
        Assert.NotNull(typeof(ApplicationDevelopmentPublishResponse).GetProperty("PublishedManifestHash"));
    }

    [Fact]
    public void Page_detail_runtime_metadata_follows_the_document_published_artifact_pointer()
    {
        var root = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AsterERP.sln"))) root = root.Parent;
        var source = File.ReadAllText(Path.Combine(root!.FullName, "backend", "AsterERP.Api", "Application", "ApplicationDevelopmentCenter", "ApplicationDevelopmentCenterService.cs"));

        Assert.Contains("item.Id == document.PublishedArtifactId", source, StringComparison.Ordinal);
        Assert.Contains("PublishedArtifactJson = publishedArtifact?.ArtifactJson", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemPageSchemaEntity", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UpsertPublishedPageSchemaAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OrderBy(item => item.PublishedTime, OrderByType.Desc)", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Permission_seed_is_idempotent_and_owns_designer_permissions()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:application-development-center-permissions-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        db.CodeFirst.InitTables<SystemPermissionCodeEntity>();

        var seeder = new ApplicationDevelopmentCenterSeedService(db);
        await seeder.SeedAsync(CancellationToken.None);
        await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(6, db.Queryable<SystemPermissionCodeEntity>()
            .Where(item => item.ModuleName == "ApplicationDevelopmentCenter" && !item.IsDeleted)
            .Count());
        Assert.Equal(1, db.Queryable<SystemPermissionCodeEntity>()
            .Where(item => item.PermissionCode == AsterERP.Shared.PermissionCodes.AppDevelopmentCenterDesignerPublish)
            .Count());
    }
}

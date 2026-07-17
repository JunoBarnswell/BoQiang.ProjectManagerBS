using AsterERP.Api.Infrastructure.Abp.Im;
using AsterERP.Api.Application.Im;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.Im;
using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Shared;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ImAbpModuleTests
{
    [Fact]
    public void Im_is_an_abp_module()
    {
        Assert.True(typeof(AbpModule).IsAssignableFrom(typeof(AsterErpImModule)));
    }

    [Fact]
    public void Im_registers_services_schema_and_seed_directly()
    {
        var services = new ServiceCollection();

        new AsterErpImModule().ConfigureServices(new ServiceConfigurationContext(services));

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IImConversationService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ImSchemaMigrator));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ImSeedService));
    }

    [Fact]
    public async Task Schema_migrator_is_idempotent_and_creates_all_tables_and_indexes()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:im-schema-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });

        var migrator = new ImSchemaMigrator();
        await migrator.MigrateAsync(db, CancellationToken.None);
        await migrator.MigrateAsync(db, CancellationToken.None);

        Assert.Equal(5, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name LIKE 'im_%'"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'index' AND name = 'ux_im_messages_client'"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'index' AND name = 'idx_im_delivery_logs_message'"));
    }

    [Fact]
    public void Im_registers_all_tenant_data_filters()
    {
        var registry = new DataPermissionFilterRegistry();

        AsterErpImModule.RegisterDataFilters(registry);

        Assert.Equal(5, registry.ImTenantEntityTypes.Count);
        Assert.Contains(typeof(ImAccountBindingEntity), registry.ImTenantEntityTypes);
        Assert.Contains(typeof(ImConversationEntity), registry.ImTenantEntityTypes);
        Assert.Contains(typeof(ImConversationParticipantEntity), registry.ImTenantEntityTypes);
        Assert.Contains(typeof(ImMessageEntity), registry.ImTenantEntityTypes);
        Assert.Contains(typeof(ImMessageDeliveryLogEntity), registry.ImTenantEntityTypes);
    }

    [Fact]
    public async Task Permission_seed_is_idempotent_and_owns_all_im_permissions()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:im-permissions-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        db.CodeFirst.InitTables<SystemPermissionCodeEntity>();

        var seeder = new ImSeedService(db);
        await seeder.SeedAsync(CancellationToken.None);
        await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(5, db.Queryable<SystemPermissionCodeEntity>()
            .Where(item => item.ModuleName == "IM" && !item.IsDeleted)
            .Count());
        Assert.Equal(1, db.Queryable<SystemPermissionCodeEntity>()
            .Where(item => item.PermissionCode == AsterERP.Shared.PermissionCodes.ImMessageSend)
            .Count());
    }

    [Fact]
    public void Im_controller_endpoints_are_bound_to_seeded_permission_codes()
    {
        AssertPermission<ImAccountController>(nameof(ImAccountController.GetCurrentAsync), PermissionCodes.ImConversationView);
        AssertPermission<ImConversationsController>(nameof(ImConversationsController.GetConversationsAsync), PermissionCodes.ImConversationView);
        AssertPermission<ImConversationsController>(nameof(ImConversationsController.CreateDirectAsync), PermissionCodes.ImConversationCreate);
        AssertPermission<ImConversationsController>(nameof(ImConversationsController.GetMessagesAsync), PermissionCodes.ImMessageRead);
        AssertPermission<ImConversationsController>(nameof(ImConversationsController.SendMessageAsync), PermissionCodes.ImMessageSend);
        AssertPermission<ImConversationsController>(nameof(ImConversationsController.MarkReadAsync), PermissionCodes.ImMessageRead);
        AssertPermission<ImUnreadController>(nameof(ImUnreadController.GetUnreadSummaryAsync), PermissionCodes.ImMessageRead);
        AssertPermission<ImUsersController>(nameof(ImUsersController.GetDirectoryAsync), PermissionCodes.ImUserSearch);
        AssertPermission<ImUsersController>(nameof(ImUsersController.SearchAsync), PermissionCodes.ImUserSearch);
    }

    private static void AssertPermission<TController>(string methodName, string expectedCode)
    {
        var method = typeof(TController).GetMethod(methodName);
        Assert.NotNull(method);
        Assert.Contains(method!.GetCustomAttributes(typeof(PermissionAttribute), inherit: true),
            attribute => ((PermissionAttribute)attribute).Code == expectedCode);
    }
}

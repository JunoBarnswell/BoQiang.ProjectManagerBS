using System.Security.Claims;
using System.Text.Json;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Files;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Hosting;
using SqlSugar;
using Volo.Abp.BackgroundJobs;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementDataSpaceImportServiceTests
{
    [Fact]
    public async Task Start_rejects_a_package_for_another_workspace_before_creating_an_operation()
    {
        using var db = CreateDatabase();
        await SeedUserAsync(db);
        var queue = new RecordingBackgroundJobManager();
        var service = CreateService(db, queue);
        await SeedReadyExportAsync(db, "export-invalid", new ProjectManagementDataSpaceExportManifest(
            1, "tenant-other", "SYSTEM", "SQLite", "OnlineBackup", 5, DateTime.UtcNow, [], "hash", "AES-256-CBC"));

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.StartAsync(
            new ProjectManagementDataSpaceImportRequest("export-invalid", "secret", true, "preflight")));

        Assert.Empty(queue.Args);
        Assert.Empty(await db.Queryable<ProjectManagementOperationEntity>().Where(item => item.Id != "").ToListAsync());
    }

    [Fact]
    public async Task Start_creates_a_pending_operation_and_enqueues_only_after_preflight_passes()
    {
        using var db = CreateDatabase();
        await SeedUserAsync(db);
        var queue = new RecordingBackgroundJobManager();
        var service = CreateService(db, queue);
        await SeedReadyExportAsync(db, "export-valid", new ProjectManagementDataSpaceExportManifest(
            1, "tenant-a", "SYSTEM", "SQLite", "OnlineBackup", 5, DateTime.UtcNow, [], "hash", "AES-256-CBC"));

        var result = await service.StartAsync(new ProjectManagementDataSpaceImportRequest("export-valid", "secret", true, "validated import"));

        Assert.Equal("Pending", result.Status);
        Assert.Equal("export-valid", result.ExportId);
        var operation = await db.Queryable<ProjectManagementOperationEntity>().Where(item => item.Id == result.OperationId).FirstAsync();
        Assert.Equal("data-space.database-import", operation.OperationType);
        Assert.Equal("Pending", operation.Status);
        Assert.Single(queue.Args);
        Assert.Equal(result.OperationId, queue.Args[0].OperationId);
    }

    private static SqlSugarClient CreateDatabase()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-data-space-import-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None).GetAwaiter().GetResult();
        db.CodeFirst.InitTables<SystemUserEntity>();
        return db;
    }

    private static async Task SeedUserAsync(ISqlSugarClient db) => await db.Insertable(new SystemUserEntity
    {
        Id = "operator", UserName = "operator", PasswordHash = "secret", Status = "Enabled"
    }).ExecuteCommandAsync();

    private static async Task SeedReadyExportAsync(ISqlSugarClient db, string id, ProjectManagementDataSpaceExportManifest manifest) => await db.Insertable(new ProjectManagementDataSpaceExportEntity
    {
        Id = id,
        TenantId = "tenant-a",
        AppCode = "SYSTEM",
        PackageName = $"{id}.bqdbx",
        ManifestJson = JsonSerializer.Serialize(manifest),
        Status = "Ready",
        CreatedByUserId = "operator",
        DownloadExpiresAt = DateTime.UtcNow.AddHours(1),
        CreatedBy = "operator",
        CreatedTime = DateTime.UtcNow
    }).ExecuteCommandAsync();

    private static ProjectManagementDataSpaceImportService CreateService(ISqlSugarClient db, IBackgroundJobManager queue)
    {
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var user = CreateUser();
        var root = Path.Combine(Path.GetTempPath(), $"project-management-import-test-{Guid.NewGuid():N}");
        return new ProjectManagementDataSpaceImportService(
            accessor,
            user,
            new ProjectManagementRiskConfirmationService(accessor, user, new TestPasswordHashService()),
            new ProjectManagementMaintenanceLock(accessor, user),
            new ProjectManagementOperationWriter(accessor, user),
            queue,
            null!,
            DataProtectionProvider.Create(Path.Combine(root, "keys")),
            new TestHostEnvironment(root));
    }

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity([
        new Claim(AsterErpClaimTypes.UserId, "operator"),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"),
        new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementDataSpaceImport)
    ], "test")));

    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient GetProjectManagementDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> GetProjectManagementDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }

    private sealed class TestPasswordHashService : IPasswordHashService
    {
        public string HashPassword(string password) => password;
        public PasswordVerificationResult Verify(string storedPassword, string inputPassword) => new(true, false, "Success", "test");
    }

    private sealed class RecordingBackgroundJobManager : IBackgroundJobManager
    {
        public List<ProjectManagementOperationJobArgs> Args { get; } = [];

        public Task<string> EnqueueAsync<TArgs>(TArgs args, BackgroundJobPriority priority = BackgroundJobPriority.Normal, TimeSpan? delay = null)
        {
            if (args is ProjectManagementOperationJobArgs operationArgs) Args.Add(operationArgs);
            return Task.FromResult(Guid.NewGuid().ToString("N"));
        }
    }

    private sealed class TestHostEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "AsterERP.Api.Tests";
        public string ContentRootPath { get; set; } = root;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}

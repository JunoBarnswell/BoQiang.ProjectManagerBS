using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.ProjectManagement;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementBackupServiceTests
{
    [Fact]
    public async Task Sqlite_backup_restore_verifies_hash_and_restores_after_mutation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"project-management-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "workspace.db");
        try
        {
            using var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={databasePath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = false
            });
            await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
            db.CodeFirst.InitTables<SystemUserEntity>();
            await db.Insertable(new SystemUserEntity { Id = "operator", UserName = "operator", PasswordHash = "secret", Status = "Enabled" }).ExecuteCommandAsync();
            db.Ado.ExecuteCommand("CREATE TABLE IF NOT EXISTS backup_marker (Id INTEGER PRIMARY KEY, Value TEXT NOT NULL);");
            db.Ado.ExecuteCommand("INSERT INTO backup_marker(Id, Value) VALUES (1, 'before');");

            var accessor = new TestWorkspaceDatabaseAccessor(db);
            var user = new FixedAsterErpCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(AsterErpClaimTypes.UserId, "operator"),
                new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
                new Claim(AsterErpClaimTypes.AppCode, "MES")
            }, "test")));
            var operationWriter = new ProjectManagementOperationWriter(accessor, user);
            var service = new ProjectManagementBackupService(accessor, user, new ProjectManagementRiskConfirmationService(accessor, user, new TestPasswordHashService()), new ProjectManagementMaintenanceLock(accessor, user), new TestHostEnvironment(root), operationWriter);

            var backup = await service.CreateAsync(new ProjectManagementBackupRequest("secret", true, "test backup"));
            Assert.Equal("Ready", backup.Status);
            db.Ado.ExecuteCommand("UPDATE backup_marker SET Value = 'after' WHERE Id = 1;");
            await service.RestoreAsync(backup.Id, new ProjectManagementRestoreRequest("secret", true));
            Assert.Equal("before", db.Ado.GetString("SELECT Value FROM backup_marker WHERE Id = 1"));
            var backups = await service.ListAsync();
            Assert.Contains(backups, item => item.Id == backup.Id);
            var safetyCheckpoint = Assert.Single(backups, item => item.Id != backup.Id);
            Assert.StartsWith("恢复前安全检查点-", safetyCheckpoint.BackupName, StringComparison.Ordinal);

            await service.RestoreAsync(safetyCheckpoint.Id, new ProjectManagementRestoreRequest("secret", true));
            Assert.Equal("after", db.Ado.GetString("SELECT Value FROM backup_marker WHERE Id = 1"));
            var operations = await db.Queryable<ProjectManagementOperationEntity>().Where(item => !item.IsDeleted).ToListAsync();
            Assert.Contains(operations, item => item.OperationType == "backup.restore" && item.Status == "Succeeded");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task Restore_rejects_backup_relative_path_traversal_before_file_access()
    {
        var root = Path.Combine(Path.GetTempPath(), $"project-management-backup-path-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-backup-path-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
            await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
            db.CodeFirst.InitTables<SystemUserEntity>();
            await db.Insertable(new SystemUserEntity { Id = "operator", UserName = "operator", PasswordHash = "secret", Status = "Enabled" }).ExecuteCommandAsync();
            await db.Insertable(new ProjectManagementBackupEntity
            {
                Id = "malicious-backup", TenantId = "tenant-a", AppCode = "MES", BackupName = "malicious",
                RelativePath = "data/project-management-backups/tenant-a/MES/../../../../outside.db",
                Sha256 = "", FileSize = 0, Status = "Ready", CreatedByUserId = "operator", CreatedBy = "operator", CreatedTime = DateTime.UtcNow
            }).ExecuteCommandAsync();

            var accessor = new TestWorkspaceDatabaseAccessor(db);
            var user = new FixedAsterErpCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(AsterErpClaimTypes.UserId, "operator"), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "MES")
            }, "test")));
            var service = new ProjectManagementBackupService(accessor, user, new ProjectManagementRiskConfirmationService(accessor, user, new TestPasswordHashService()), new ProjectManagementMaintenanceLock(accessor, user), new TestHostEnvironment(root));

            await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.RestoreAsync("malicious-backup", new ProjectManagementRestoreRequest("secret", true)));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task Create_records_failed_operation_when_backup_creation_fails_after_start()
    {
        var rootFile = Path.Combine(Path.GetTempPath(), $"project-management-backup-root-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(rootFile, "not a directory");
        try
        {
            using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-backup-failure-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
            await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
            db.CodeFirst.InitTables<SystemUserEntity>();
            await db.Insertable(new SystemUserEntity { Id = "operator", UserName = "operator", PasswordHash = "secret", Status = "Enabled" }).ExecuteCommandAsync();
            var accessor = new TestWorkspaceDatabaseAccessor(db);
            var user = new FixedAsterErpCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(AsterErpClaimTypes.UserId, "operator"), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "MES")
            }, "test")));
            var service = new ProjectManagementBackupService(accessor, user, new ProjectManagementRiskConfirmationService(accessor, user, new TestPasswordHashService()), new ProjectManagementMaintenanceLock(accessor, user), new TestHostEnvironment(rootFile), new ProjectManagementOperationWriter(accessor, user));

            await Assert.ThrowsAnyAsync<Exception>(() => service.CreateAsync(new ProjectManagementBackupRequest("secret", true)));
            var operation = await db.Queryable<ProjectManagementOperationEntity>().FirstAsync();
            Assert.Equal("Failed", operation.Status);
            Assert.False(string.IsNullOrWhiteSpace(operation.ErrorMessage));
        }
        finally
        {
            try { File.Delete(rootFile); } catch { }
        }
    }

    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }

    private sealed class TestPasswordHashService : IPasswordHashService
    {
        public string HashPassword(string password) => password;
        public PasswordVerificationResult Verify(string storedPassword, string inputPassword) => new(true, false, "Success", "test");
    }

    private sealed class TestHostEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

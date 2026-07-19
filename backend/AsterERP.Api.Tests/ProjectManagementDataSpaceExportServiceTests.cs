using System.IO.Compression;
using System.Security.Claims;
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
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementDataSpaceExportServiceTests
{
    [Fact]
    public async Task Execute_creates_encrypted_package_with_manifest_and_limited_download_lifecycle()
    {
        var root = Path.Combine(Path.GetTempPath(), $"project-management-data-space-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var databasePath = Path.Combine(root, "workspace.db");
        try
        {
            using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source={databasePath}", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
            await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
            db.CodeFirst.InitTables<SystemUserEntity>();
            await db.Insertable(new SystemUserEntity { Id = "operator", UserName = "operator", PasswordHash = "secret", Status = "Enabled" }).ExecuteCommandAsync();
            await db.Insertable(new ProjectManagementProjectEntity { Id = "project-1", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "P-1", ProjectName = "Export target", OwnerUserId = "operator" }).ExecuteCommandAsync();

            var accessor = new TestWorkspaceDatabaseAccessor(db);
            var user = CreateUser(includeExportPermission: true);
            var writer = new ProjectManagementOperationWriter(accessor, user);
            const string operationId = "operation-1";
            const string exportId = "export-1";
            await db.Insertable(new ProjectManagementDataSpaceExportEntity
            {
                Id = exportId, TenantId = "tenant-a", AppCode = "SYSTEM", OperationId = operationId,
                PackageName = "workspace.bqdbx", Status = "Pending", CreatedByUserId = "operator",
                DownloadExpiresAt = DateTime.UtcNow.AddHours(1), MaxDownloadCount = 1, CreatedBy = "operator", CreatedTime = DateTime.UtcNow
            }).ExecuteCommandAsync();
            await writer.CreatePendingAsync(operationId, "data-space.database-export", "{}", "trace", CancellationToken.None);
            var service = new ProjectManagementDataSpaceExportService(
                accessor, user,
                new ProjectManagementRiskConfirmationService(accessor, user, new TestPasswordHashService()),
                writer,
                null!,
                new TestFileStorageService(Path.Combine(root, "blobs")),
                DataProtectionProvider.Create(Path.Combine(root, "keys")));

            await service.ExecuteAsync(operationId);

            var export = await db.Queryable<ProjectManagementDataSpaceExportEntity>().Where(item => item.Id == exportId).FirstAsync();
            var operation = await db.Queryable<ProjectManagementOperationEntity>().Where(item => item.Id == operationId).FirstAsync();
            Assert.True(string.Equals("Ready", export.Status, StringComparison.Ordinal), operation.ErrorMessage);
            Assert.NotEmpty(export.PackageSha256);
            Assert.NotEmpty(export.DatabaseSha256);
            Assert.NotEmpty(export.EncryptionKeyCipherText);
            Assert.True(export.PackageSize > 0);
            await using (var stored = await new TestFileStorageService(Path.Combine(root, "blobs")).OpenReadAsync(export.StoragePath))
            using (var package = new ZipArchive(stored, ZipArchiveMode.Read))
            {
                Assert.NotNull(package.GetEntry("manifest.json"));
                Assert.NotNull(package.GetEntry("database.sqlite.aes"));
            }

            var download = await service.DownloadAsync(exportId);
            Assert.Equal("workspace.bqdbx", download.FileName);
            Assert.True(download.Stream.Length > 0);
            await download.Stream.DisposeAsync();
            await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.DownloadAsync(exportId));
            Assert.Equal(1, (await db.Queryable<ProjectManagementDataSpaceExportEntity>().Where(item => item.Id == exportId).FirstAsync()).DownloadCount);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task Execute_rejects_caller_without_explicit_export_permission()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-data-space-export-denied-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var user = CreateUser(includeExportPermission: false);
        var service = new ProjectManagementDataSpaceExportService(accessor, user,
            new ProjectManagementRiskConfirmationService(accessor, user, new TestPasswordHashService()),
            new ProjectManagementOperationWriter(accessor, user), null!, new TestFileStorageService(Path.GetTempPath()), DataProtectionProvider.Create(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.ListAsync());
    }

    private static FixedAsterErpCurrentUser CreateUser(bool includeExportPermission)
    {
        var claims = new List<Claim>
        {
            new(AsterErpClaimTypes.UserId, "operator"), new(AsterErpClaimTypes.TenantId, "tenant-a"), new(AsterErpClaimTypes.AppCode, "SYSTEM")
        };
        if (includeExportPermission) claims.Add(new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementDataSpaceExport));
        return new FixedAsterErpCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")));
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

    private sealed class TestFileStorageService(string root) : IFileStorageService
    {
        public async Task<string> SaveAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(root);
            var name = $"{Guid.NewGuid():N}-{fileName}";
            await using var target = File.Create(Path.Combine(root, name));
            await stream.CopyToAsync(target, cancellationToken);
            return name;
        }

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(File.OpenRead(Path.Combine(root, relativePath)));

        public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        {
            File.Delete(Path.Combine(root, relativePath));
            return Task.CompletedTask;
        }
    }
}

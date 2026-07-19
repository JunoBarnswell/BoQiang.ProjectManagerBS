using System.IO.Compression;
using System.Security.Claims;
using System.Text.Json.Nodes;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementSyncValidationTests
{
    [Fact]
    public async Task Preview_validates_v3_signature_resources_and_does_not_write_import_records()
    {
        using var db = CreateDatabase("validation");
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "validation-project", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "VALIDATION",
            ProjectName = "Validation", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var service = new ProjectManagementSyncService(accessor, CreateUser(), new ProjectManagementAccessPolicy(accessor, CreateUser()), new TestPasswordHashService());
        var exported = await service.ExportAsync(new ProjectManagementSyncExportRequest("validation-project"));
        var operationCount = await db.Queryable<ProjectManagementOperationEntity>().CountAsync();

        var preview = await service.PreviewAsync(new MemoryStream(exported.Content));

        Assert.True(preview.IsCompatible);
        Assert.True(preview.SignatureValid);
        Assert.Equal("Valid", preview.ValidationState);
        Assert.True(preview.PreviewOnly);
        Assert.True(preview.UncompressedSize > 0);
        Assert.Equal(operationCount, await db.Queryable<ProjectManagementOperationEntity>().CountAsync());

        var tamperedManifest = RewritePackage(exported.Content, addUnknownEntry: false, mutateManifest: true);
        var tamperedPreview = await service.PreviewAsync(new MemoryStream(tamperedManifest));
        Assert.False(tamperedPreview.IsCompatible);
        Assert.False(tamperedPreview.SignatureValid);

        var unknownEntryPackage = RewritePackage(exported.Content, addUnknownEntry: true, mutateManifest: false);
        await Assert.ThrowsAsync<ValidationException>(() => service.PreviewAsync(new MemoryStream(unknownEntryPackage)));
    }

    private static SqlSugarClient CreateDatabase(string suffix) => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:project-management-sync-{suffix}-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

    private static byte[] RewritePackage(byte[] source, bool addUnknownEntry, bool mutateManifest)
    {
        using var input = new ZipArchive(new MemoryStream(source), ZipArchiveMode.Read);
        using var outputStream = new MemoryStream();
        using (var output = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var sourceEntry in input.Entries)
            {
                var bytes = ReadEntry(sourceEntry);
                if (mutateManifest && string.Equals(sourceEntry.FullName, "manifest.json", StringComparison.Ordinal))
                {
                    var manifest = JsonNode.Parse(bytes)!.AsObject();
                    manifest["signature"] = "00";
                    bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(manifest);
                }
                using var target = output.CreateEntry(sourceEntry.FullName).Open();
                target.Write(bytes);
            }
            if (addUnknownEntry)
            {
                using var target = output.CreateEntry("unexpected.json").Open();
                target.Write(new byte[] { 1 });
            }
        }
        return outputStream.ToArray();
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        using var input = entry.Open();
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "SYSTEM")
    }, "test")));

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
}

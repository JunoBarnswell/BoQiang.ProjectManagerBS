using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementTaskTemplateServiceTests
{
    [Fact]
    public async Task Applying_same_occurrence_is_idempotent_and_preserves_parent_tree()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        var service = new ProjectManagementTaskTemplateService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var template = await service.CreateAsync("project-a", new ProjectManagementTaskTemplateUpsertRequest(
            "release", "Release", "[{\"TaskCode\":\"root\",\"Title\":\"Root\",\"Weight\":2},{\"TaskCode\":\"child\",\"Title\":\"Child\",\"ParentCode\":\"root\",\"DueDays\":1}]"));

        var first = await service.ApplyAsync(template.Id, new ProjectManagementTaskTemplateApplyRequest("project-a", "2026-07-18", new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc)));
        var second = await service.ApplyAsync(template.Id, new ProjectManagementTaskTemplateApplyRequest("project-a", "2026-07-18", new DateTime(2026, 7, 18, 0, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(2, first.Count);
        Assert.Equal(first.Select(item => item.Id).OrderBy(item => item), second.Select(item => item.Id).OrderBy(item => item));
        Assert.Equal(1, await db.Queryable<ProjectManagementTaskOccurrenceEntity>().CountAsync());
        Assert.Equal(1, await db.Queryable<ProjectManagementTaskEntity>().CountAsync(item => item.ParentTaskId == first.Single(item => item.TaskCode.StartsWith("root-", StringComparison.Ordinal)).Id));
        Assert.All(await db.Queryable<ProjectManagementTaskEntity>().ToListAsync(), task => Assert.Equal("2026-07-18", task.OccurrenceKey));
    }

    [Fact]
    public async Task Template_definition_rejects_duplicate_and_missing_parent_codes()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "MES", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
        var service = new ProjectManagementTaskTemplateService(new TestWorkspaceDatabaseAccessor(db), CreateUser());

        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync("project-a", new ProjectManagementTaskTemplateUpsertRequest("bad", "Bad", "[{\"TaskCode\":\"a\",\"Title\":\"A\"},{\"TaskCode\":\"a\",\"Title\":\"A2\"}]")));
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.CreateAsync("project-a", new ProjectManagementTaskTemplateUpsertRequest("bad", "Bad", "[{\"TaskCode\":\"a\",\"Title\":\"A\",\"ParentCode\":\"missing\"}]")));
    }

    private static SqlSugarClient CreateDb() => new(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-template-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "MES")
    }, "test")));
    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }
}

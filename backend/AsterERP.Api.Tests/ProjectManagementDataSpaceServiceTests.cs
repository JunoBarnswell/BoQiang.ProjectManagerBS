using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.Extensions.Caching.Memory;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementDataSpaceServiceTests
{
    [Fact]
    public async Task Summary_aggregates_in_database_and_does_not_leak_inaccessible_project_counts()
    {
        using var db = CreateDatabase();
        await SeedAsync(db);
        var currentUser = CreateUser("member");
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ProjectManagementDataSpaceService(new TestWorkspaceDatabaseAccessor(db), currentUser, cache);

        var summary = await service.GetSummaryAsync();

        Assert.Equal("Healthy", summary.DatabaseStatus);
        Assert.True(summary.IsStatisticsScoped);
        Assert.Equal(1, summary.ProjectCount);
        Assert.Equal(1, summary.TaskCount);
        Assert.Equal(2, summary.MemberCount);
        Assert.Equal(1, summary.MilestoneCount);
        Assert.Equal(1, summary.AttachmentCount);
        Assert.Single(summary.AvailableDataSpaces);
        Assert.True(summary.AvailableDataSpaces[0].IsCurrent);
        Assert.DoesNotContain("hidden", summary.DataSpaceName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Summary_returns_platform_wide_counts_for_platform_administrator()
    {
        using var db = CreateDatabase();
        await SeedAsync(db);
        var currentUser = CreateUser("administrator", isPlatformAdministrator: true);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ProjectManagementDataSpaceService(new TestWorkspaceDatabaseAccessor(db), currentUser, cache);

        var summary = await service.GetSummaryAsync();

        Assert.False(summary.IsStatisticsScoped);
        Assert.Equal(2, summary.ProjectCount);
        Assert.Equal(2, summary.TaskCount);
        Assert.Equal(3, summary.MemberCount);
        Assert.Equal(2, summary.MilestoneCount);
        Assert.Equal(2, summary.AttachmentCount);
    }

    [Fact]
    public async Task Summary_returns_an_actionable_unavailable_state_when_the_data_space_cannot_be_opened()
    {
        var currentUser = CreateUser("administrator", isPlatformAdministrator: true);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ProjectManagementDataSpaceService(new UnavailableWorkspaceDatabaseAccessor(), currentUser, cache);

        var summary = await service.GetSummaryAsync();

        Assert.Equal("Unavailable", summary.DatabaseStatus);
        Assert.Equal(0, summary.ProjectCount);
        Assert.Equal("/platform/project-management/operations", summary.HandlingRoute);
        Assert.Contains("不可达", summary.StatusMessage);
        Assert.Empty(summary.AvailableDataSpaces);
    }

    private static SqlSugarClient CreateDatabase()
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-data-space-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None).GetAwaiter().GetResult();
        return db;
    }

    private static async Task SeedAsync(ISqlSugarClient db)
    {
        var now = DateTime.UtcNow;
        await db.Insertable(new[]
        {
            new ProjectManagementProjectEntity { Id = "visible", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "VISIBLE", ProjectName = "Visible", OwnerUserId = "owner" },
            new ProjectManagementProjectEntity { Id = "hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "HIDDEN", ProjectName = "Hidden", OwnerUserId = "other" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementProjectMemberEntity { Id = "visible-member", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "visible", UserId = "member", RoleCode = "Member", IsActive = true },
            new ProjectManagementProjectMemberEntity { Id = "visible-owner", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "visible", UserId = "owner", RoleCode = "Owner", IsActive = true },
            new ProjectManagementProjectMemberEntity { Id = "hidden-owner", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "hidden", UserId = "other", RoleCode = "Owner", IsActive = true }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "visible-task", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "visible", TaskCode = "VT", Title = "Visible task" },
            new ProjectManagementTaskEntity { Id = "hidden-task", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "hidden", TaskCode = "HT", Title = "Hidden task" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementMilestoneEntity { Id = "visible-milestone", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "visible", MilestoneName = "Visible milestone" },
            new ProjectManagementMilestoneEntity { Id = "hidden-milestone", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "hidden", MilestoneName = "Hidden milestone" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskAttachmentEntity { Id = "visible-attachment", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "visible", TaskId = "visible-task", FileId = "f-visible", FileName = "visible.txt" },
            new ProjectManagementTaskAttachmentEntity { Id = "hidden-attachment", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "hidden", TaskId = "hidden-task", FileId = "f-hidden", FileName = "hidden.txt" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementActivityEntity { Id = "visible-activity", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "visible", AggregateType = "Project", AggregateId = "visible", ActivityType = "created", TraceId = "visible-trace", ActorUserId = "owner", CreatedBy = "owner", CreatedTime = now },
            new ProjectManagementActivityEntity { Id = "hidden-activity", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "hidden", AggregateType = "Project", AggregateId = "hidden", ActivityType = "created", TraceId = "hidden-trace", ActorUserId = "other", CreatedBy = "other", CreatedTime = now.AddMinutes(1) }
        }).ExecuteCommandAsync();
    }

    private static FixedAsterErpCurrentUser CreateUser(string userId, bool isPlatformAdministrator = false) => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, userId),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"),
        new Claim(AsterErpClaimTypes.IsPlatformAdmin, isPlatformAdministrator.ToString())
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

    private sealed class UnavailableWorkspaceDatabaseAccessor : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => throw new InvalidOperationException("database unavailable");

        public ISqlSugarClient GetCurrentDb() => throw new InvalidOperationException("database unavailable");

        public ISqlSugarClient GetProjectManagementDb() => throw new InvalidOperationException("database unavailable");

        public ISqlSugarClient RequireApplicationDb() => throw new InvalidOperationException("database unavailable");

        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("database unavailable");

        public Task<ISqlSugarClient> GetProjectManagementDbAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("database unavailable");

        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("database unavailable");
    }
}

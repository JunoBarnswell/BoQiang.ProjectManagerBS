using System.Security.Claims;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.ProjectManagement;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementDataPermissionFilterTests
{
    [Fact]
    public async Task Project_filter_returns_only_current_workspace_projects_visible_to_member()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-filter-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new AsterERP.Api.Infrastructure.Abp.ProjectManagement.ProjectManagementSchemaMigrator()
            .MigrateAsync(db, CancellationToken.None);

        await db.Insertable(new[]
        {
            new ProjectManagementProjectEntity { Id = "project-visible", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "VISIBLE", ProjectName = "Visible", OwnerUserId = "owner" },
            new ProjectManagementProjectEntity { Id = "project-hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "HIDDEN", ProjectName = "Hidden", OwnerUserId = "other" },
            new ProjectManagementProjectEntity { Id = "project-other", TenantId = "tenant-b", AppCode = "SYSTEM", ProjectCode = "OTHER", ProjectName = "Other", OwnerUserId = "operator" },
            new ProjectManagementProjectEntity { Id = "project-other-app", TenantId = "tenant-a", AppCode = "CRM", ProjectCode = "OTHER_APP", ProjectName = "Other app", OwnerUserId = "operator" }
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectMemberEntity
        {
            Id = "member-visible",
            TenantId = "tenant-a",
            AppCode = "SYSTEM",
            ProjectId = "project-visible",
            UserId = "operator",
            IsActive = true
        }).ExecuteCommandAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AsterErpClaimTypes.UserId, "operator"),
            new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
            new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"),
            new Claim(AsterErpClaimTypes.DataScope, "SELF"),
            new Claim(AsterErpClaimTypes.PermissionCode, "project-management:project:view")
        }, "test"));

        ProjectManagementDataPermissionFilterRegistrar.TryRegister(
            db,
            typeof(ProjectManagementProjectEntity),
            new FixedAsterErpCurrentUser(principal),
            "tenant-a",
            "SYSTEM");

        var visible = await db.Queryable<ProjectManagementProjectEntity>()
            .OrderBy(item => item.ProjectCode)
            .ToListAsync();

        var project = Assert.Single(visible);
        Assert.Equal("project-visible", project.Id);
    }

    [Fact]
    public async Task Related_project_entities_are_filtered_by_the_same_visible_project_subquery()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-related-filter-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new AsterERP.Api.Infrastructure.Abp.ProjectManagement.ProjectManagementSchemaMigrator()
            .MigrateAsync(db, CancellationToken.None);

        await db.Insertable(new[]
        {
            new ProjectManagementProjectEntity { Id = "project-visible", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "VISIBLE", ProjectName = "Visible", OwnerUserId = "operator" },
            new ProjectManagementProjectEntity { Id = "project-hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "HIDDEN", ProjectName = "Hidden", OwnerUserId = "other" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementMilestoneEntity { Id = "milestone-visible", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-visible", MilestoneName = "Visible milestone" },
            new ProjectManagementMilestoneEntity { Id = "milestone-hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-hidden", MilestoneName = "Hidden milestone" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "task-visible", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-visible", TaskCode = "T-V", Title = "Visible task" },
            new ProjectManagementTaskEntity { Id = "task-hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-hidden", TaskCode = "T-H", Title = "Hidden task" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskDependencyEntity { Id = "dependency-visible", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-visible", PredecessorTaskId = "task-visible", SuccessorTaskId = "task-visible" },
            new ProjectManagementTaskDependencyEntity { Id = "dependency-hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-hidden", PredecessorTaskId = "task-hidden", SuccessorTaskId = "task-hidden" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskParticipantEntity { Id = "participant-visible", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-visible", TaskId = "task-visible", UserId = "operator" },
            new ProjectManagementTaskParticipantEntity { Id = "participant-hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-hidden", TaskId = "task-hidden", UserId = "operator" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskRecurrenceEntity { Id = "recurrence-visible", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-visible", SourceTaskId = "task-visible", Frequency = "Daily" },
            new ProjectManagementTaskRecurrenceEntity { Id = "recurrence-hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-hidden", SourceTaskId = "task-hidden", Frequency = "Daily" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskRecurrenceOccurrenceEntity { Id = "recurrence-occurrence-visible", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-visible", RecurrenceId = "recurrence-visible", TaskId = "task-visible", RecurrenceKey = "visible" },
            new ProjectManagementTaskRecurrenceOccurrenceEntity { Id = "recurrence-occurrence-hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-hidden", RecurrenceId = "recurrence-hidden", TaskId = "task-hidden", RecurrenceKey = "hidden" }
        }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementImConversationLinkEntity { Id = "link-visible", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-visible", ConversationKey = "pm:visible", Status = "Active" },
            new ProjectManagementImConversationLinkEntity { Id = "link-hidden", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-hidden", ConversationKey = "pm:hidden", Status = "Active" }
        }).ExecuteCommandAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AsterErpClaimTypes.UserId, "operator"),
            new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
            new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"),
            new Claim(AsterErpClaimTypes.DataScope, "SELF"),
            new Claim(AsterErpClaimTypes.PermissionCode, "project-management:project:view")
        }, "test"));
        var currentUser = new FixedAsterErpCurrentUser(principal);

        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementMilestoneEntity), currentUser, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskEntity), currentUser, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskDependencyEntity), currentUser, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskParticipantEntity), currentUser, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskRecurrenceEntity), currentUser, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskRecurrenceOccurrenceEntity), currentUser, "tenant-a", "SYSTEM"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementImConversationLinkEntity), currentUser, "tenant-a", "SYSTEM"));

        Assert.Equal("milestone-visible", Assert.Single(await db.Queryable<ProjectManagementMilestoneEntity>().ToListAsync()).Id);
        Assert.Equal("task-visible", Assert.Single(await db.Queryable<ProjectManagementTaskEntity>().ToListAsync()).Id);
        Assert.Equal("dependency-visible", Assert.Single(await db.Queryable<ProjectManagementTaskDependencyEntity>().ToListAsync()).Id);
        Assert.Equal("participant-visible", Assert.Single(await db.Queryable<ProjectManagementTaskParticipantEntity>().ToListAsync()).Id);
        Assert.Equal("recurrence-visible", Assert.Single(await db.Queryable<ProjectManagementTaskRecurrenceEntity>().ToListAsync()).Id);
        Assert.Equal("recurrence-occurrence-visible", Assert.Single(await db.Queryable<ProjectManagementTaskRecurrenceOccurrenceEntity>().ToListAsync()).Id);
        Assert.Equal("link-visible", Assert.Single(await db.Queryable<ProjectManagementImConversationLinkEntity>().ToListAsync()).Id);
    }

    [Fact]
    public async Task Scoped_lead_filter_uses_current_membership_scope_immediately()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-scope-filter-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new AsterERP.Api.Infrastructure.Abp.ProjectManagement.ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "owner" }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "root-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "A", Title = "A" },
            new ProjectManagementTaskEntity { Id = "child-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", ParentTaskId = "root-a", TaskCode = "A-1", Title = "A-1" },
            new ProjectManagementTaskEntity { Id = "root-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "B", Title = "B" }
        }).ExecuteCommandAsync();
        var member = new ProjectManagementProjectMemberEntity { Id = "lead-member", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "lead", RoleCode = "Lead", ScopeRootTaskId = "root-a", IsActive = true };
        await db.Insertable(member).ExecuteCommandAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AsterErpClaimTypes.UserId, "lead"), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"), new Claim(AsterErpClaimTypes.DataScope, "SELF")
        }, "test"));
        Assert.True(ProjectManagementDataPermissionFilterRegistrar.TryRegister(db, typeof(ProjectManagementTaskEntity), new FixedAsterErpCurrentUser(principal), "tenant-a", "SYSTEM"));

        Assert.Equal(["child-a", "root-a"], (await db.Queryable<ProjectManagementTaskEntity>().OrderBy(item => item.Id).Select(item => item.Id).ToListAsync()).OrderBy(item => item).ToArray());
        member.ScopeRootTaskId = "root-b";
        await db.Updateable(member).ExecuteCommandAsync();
        Assert.Equal(["root-b"], await db.Queryable<ProjectManagementTaskEntity>().Select(item => item.Id).ToListAsync());
        member.IsActive = false;
        await db.Updateable(member).ExecuteCommandAsync();
        Assert.Empty(await db.Queryable<ProjectManagementTaskEntity>().ToListAsync());
    }
}

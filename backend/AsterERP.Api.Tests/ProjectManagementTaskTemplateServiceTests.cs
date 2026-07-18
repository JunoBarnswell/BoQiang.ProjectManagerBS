using System.Security.Claims;
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

public sealed class ProjectManagementTaskTemplateServiceTests
{
    [Fact]
    public async Task Create_from_parent_task_captures_reusable_tree_without_runtime_fields()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await SeedProjectAsync(db);
        await db.Insertable(new ProjectManagementProjectMemberEntity { Id = "member-lead", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", UserId = "source-lead", RoleCode = "Lead" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementMilestoneEntity { Id = "milestone-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", MilestoneName = "交付" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementLabelEntity { Id = "label-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", LabelName = "关键", Color = "#FF0000" }).ExecuteCommandAsync();
        await db.Insertable(new[]
        {
            new ProjectManagementTaskEntity { Id = "task-root", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskCode = "ROOT", Title = "主题", Description = "模板描述", Status = "InProgress", Priority = "High", AssigneeUserId = "source-lead", MilestoneId = "milestone-a", StartDate = new DateTime(2026, 7, 1), DueDate = new DateTime(2026, 7, 4), EstimateMinutes = 180, ActualMinutes = 99, ProgressPercent = 55, Depth = 0, SortOrder = 1024 },
            new ProjectManagementTaskEntity { Id = "task-child", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", ParentTaskId = "task-root", TaskCode = "CHILD", Title = "子任务", Status = "Todo", Priority = "Medium", Depth = 1, SortOrder = 1024 }
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskLabelEntity { Id = "task-label-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", TaskId = "task-root", LabelId = "label-a" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementTaskDependencyEntity { Id = "dependency-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", PredecessorTaskId = "task-root", SuccessorTaskId = "task-child", DependencyType = "FinishToStart", LagMinutes = 15 }).ExecuteCommandAsync();
        var service = new ProjectManagementTaskTemplateService(new TestWorkspaceDatabaseAccessor(db), CreateUser());

        var template = await service.CreateFromTaskAsync("project-a", new ProjectManagementTaskTemplateCreateFromTaskRequest("task-root", "delivery", "交付模板", IsGlobal: true));
        var definition = ProjectManagementTaskTemplateDefinitionSerializer.Parse(template.DefinitionJson);

        Assert.Equal(ProjectManagementTaskTemplateScopes.Global, template.Scope);
        Assert.Null(template.ProjectId);
        Assert.Equal(2, definition.Nodes.Count);
        var root = definition.Nodes.Single(item => item.TaskCode == "ROOT");
        var child = definition.Nodes.Single(item => item.TaskCode == "CHILD");
        Assert.Equal("Lead", root.DefaultRoleCode);
        Assert.Equal(3, root.DefaultDurationDays);
        Assert.Equal(180, root.EstimateMinutes);
        Assert.Equal("模板描述", root.Description);
        Assert.Single(root.Labels);
        Assert.Equal(root.NodeKey, child.ParentNodeKey);
        Assert.Single(definition.Milestones);
        Assert.Single(definition.Dependencies);
        Assert.Equal(root.NodeKey, definition.Dependencies[0].PredecessorNodeKey);
        Assert.Equal(child.NodeKey, definition.Dependencies[0].SuccessorNodeKey);
        Assert.DoesNotContain("ActualMinutes", template.DefinitionJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ProgressPercent", template.DefinitionJson, StringComparison.Ordinal);
        Assert.DoesNotContain("StartDate", template.DefinitionJson, StringComparison.Ordinal);
        Assert.DoesNotContain("DueDate", template.DefinitionJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Instantiate_forwards_explicit_role_mapping_without_implicit_assignment()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await SeedProjectAsync(db);
        var executor = new RecordingInstantiationService();
        var service = new ProjectManagementTaskTemplateService(new TestWorkspaceDatabaseAccessor(db), CreateUser(), instantiationService: executor);
        var template = await service.CreateAsync("project-a", new ProjectManagementTaskTemplateUpsertRequest(
            "role-template", "Role template", Json(new ProjectManagementTaskTemplateDefinition(1,
                [new ProjectManagementTaskTemplateNodeDefinition("node-1", "ROOT", "主题", null, "Todo", "Medium", "Lead", [], 60, 2, null, null, 1, 1024)], [], []))));

        var result = await service.InstantiateAsync(template.Id, new ProjectManagementTaskTemplateInstantiateRequest("project-a", RoleAssigneeMappings: new Dictionary<string, string> { ["Lead"] = "target-lead" }));

        Assert.Equal("target-lead", executor.Request!.RoleAssigneeMappings!["Lead"]);
        Assert.Equal("Lead", executor.Definition!.Nodes.Single().DefaultRoleCode);
        Assert.Single(result.Warnings);
        Assert.Equal("role-assignee-unmapped", result.Warnings[0].Code);
    }

    [Fact]
    public async Task Instantiate_creates_new_tree_labels_and_dependencies_in_target_project()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await SeedProjectAsync(db);
        await db.Insertable(new ProjectManagementProjectEntity { Id = "project-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "B", ProjectName = "B", OwnerUserId = "operator" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementProjectMemberEntity { Id = "member-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-b", UserId = "target-lead", EmploymentId = "employment-b", RoleCode = "Lead" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementMilestoneEntity { Id = "milestone-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-b", MilestoneName = "目标里程碑" }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementLabelEntity { Id = "label-b", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-b", LabelName = "关键", Color = "#FF0000" }).ExecuteCommandAsync();
        var definition = new ProjectManagementTaskTemplateDefinition(1,
            [new ProjectManagementTaskTemplateNodeDefinition("root", "ROOT", "Root", null, "Todo", "High", "Lead", [new ProjectManagementTaskTemplateLabelDefinition("label:关键:#FF0000", "关键", "#FF0000")], 60, 2, "m1", null, 1, 1024),
             new ProjectManagementTaskTemplateNodeDefinition("child", "CHILD", "Child", null, "Todo", "Medium", null, [], null, null, null, "root", 1, 1024)],
            [new ProjectManagementTaskTemplateMilestoneDefinition("m1", "源里程碑")],
            [new ProjectManagementTaskTemplateDependencyDefinition("root", "child", "FinishToStart", 0)]);
        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var user = CreateUser();
        var dependencies = new ProjectManagementTaskDependencyService(accessor, user);
        var tasks = new ProjectManagementTaskService(accessor, user, dependencies, labelMutation: new ProjectManagementTaskLabelMutation(), templateDependencyCommand: dependencies);
        var templates = new ProjectManagementTaskTemplateService(accessor, user, instantiationService: new ProjectManagementTaskTemplateInstantiationService(accessor, user, tasks));
        var template = await templates.CreateAsync("project-a", new ProjectManagementTaskTemplateUpsertRequest("tree", "Tree", Json(definition), IsGlobal: true));

        var result = await templates.InstantiateAsync(template.Id, new ProjectManagementTaskTemplateInstantiateRequest("project-b", new DateTime(2026, 8, 1), new Dictionary<string, string> { ["m1"] = "milestone-b" }, new Dictionary<string, string> { ["Lead"] = "target-lead" }));

        Assert.Equal(2, result.Tasks.Count);
        var root = result.Tasks.Single(item => item.Title == "Root");
        var child = result.Tasks.Single(item => item.Title == "Child");
        Assert.NotEqual("ROOT", root.TaskCode);
        Assert.Equal(root.Id, child.ParentTaskId);
        Assert.Equal("target-lead", root.AssigneeUserId);
        Assert.Equal("milestone-b", root.MilestoneId);
        Assert.Equal(new DateTime(2026, 8, 3), root.DueDate);
        Assert.Equal(1, await db.Queryable<ProjectManagementTaskLabelEntity>().CountAsync(item => item.TaskId == root.Id && !item.IsDeleted));
        Assert.Equal(1, await db.Queryable<ProjectManagementTaskDependencyEntity>().CountAsync(item => item.PredecessorTaskId == root.Id && item.SuccessorTaskId == child.Id && !item.IsDeleted));
    }

    [Theory]
    [InlineData("{\"SchemaVersion\":1,\"Nodes\":[{\"NodeKey\":\"a\",\"TaskCode\":\"A\",\"Title\":\"A\",\"Status\":\"Todo\",\"Priority\":\"Medium\",\"Labels\":[],\"Weight\":1,\"SortOrder\":1024},{\"NodeKey\":\"b\",\"TaskCode\":\"B\",\"Title\":\"B\",\"Status\":\"Todo\",\"Priority\":\"Medium\",\"Labels\":[],\"ParentNodeKey\":\"missing\",\"Weight\":1,\"SortOrder\":2048}],\"Milestones\":[],\"Dependencies\":[]}")]
    public async Task Template_definition_rejects_invalid_tree(string definitionJson)
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await SeedProjectAsync(db);
        var service = new ProjectManagementTaskTemplateService(new TestWorkspaceDatabaseAccessor(db), CreateUser());

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync("project-a", new ProjectManagementTaskTemplateUpsertRequest("bad", "Bad", definitionJson)));
    }

    [Fact]
    public async Task Template_version_conflict_does_not_mutate_existing_snapshot()
    {
        using var db = CreateDb();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await SeedProjectAsync(db);
        var service = new ProjectManagementTaskTemplateService(new TestWorkspaceDatabaseAccessor(db), CreateUser());
        var definition = Json(new ProjectManagementTaskTemplateDefinition(1,
            [new ProjectManagementTaskTemplateNodeDefinition("node-1", "ROOT", "旧标题", null, "Todo", "Medium", null, [], null, null, null, null, 1, 1024)], [], []));
        var template = await service.CreateAsync("project-a", new ProjectManagementTaskTemplateUpsertRequest("versioned", "Versioned", definition));

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync("project-a", template.Id,
            new ProjectManagementTaskTemplateUpsertRequest("versioned", "changed", definition, VersionNo: 99)));

        var stored = (await service.QueryAsync("project-a")).Single(item => item.Id == template.Id);
        Assert.Equal(template.DefinitionJson, stored.DefinitionJson);
        Assert.Equal(1, stored.VersionNo);
    }

    private static string Json(ProjectManagementTaskTemplateDefinition definition) => ProjectManagementTaskTemplateDefinitionSerializer.Serialize(definition);
    private static async Task SeedProjectAsync(SqlSugarClient db) => await db.Insertable(new ProjectManagementProjectEntity { Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator" }).ExecuteCommandAsync();
    private static SqlSugarClient CreateDb() => new(new ConnectionConfig { ConnectionString = $"Data Source=file:project-management-template-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", DbType = DbType.Sqlite, IsAutoCloseConnection = false });
    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"), new Claim(AsterErpClaimTypes.TenantId, "tenant-a"), new Claim(AsterErpClaimTypes.AppCode, "SYSTEM")
    }, "test")));

    private sealed class RecordingInstantiationService : IProjectManagementTaskTemplateInstantiationService
    {
        public ProjectManagementTaskTemplateInstantiateRequest? Request { get; private set; }
        public ProjectManagementTaskTemplateDefinition? Definition { get; private set; }

        public Task<ProjectManagementTaskTemplateInstantiationResponse> InstantiateAsync(ProjectManagementTaskTemplateResponse template, ProjectManagementTaskTemplateDefinition definition, ProjectManagementTaskTemplateInstantiateRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            Definition = definition;
            return Task.FromResult(new ProjectManagementTaskTemplateInstantiationResponse(template.Id, template.VersionNo, [], [new ProjectManagementTaskTemplateInstantiationWarning("role-assignee-unmapped", "node-1", "测试 warning") ]));
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
}

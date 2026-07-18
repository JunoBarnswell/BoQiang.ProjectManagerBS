using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementAuditServiceTests
{
    [Fact]
    public async Task Audit_query_and_export_use_project_scope_and_csv_escaping()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-audit-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "A", ProjectName = "A", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        await db.Insertable(new ProjectManagementActivityEntity
        {
            Id = "activity-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectId = "project-a", AggregateType = "Task", AggregateId = "task-a",
            ActivityType = "updated", Summary = "标题, \"已更新\"", TraceId = "trace-a", ActorUserId = "operator", CreatedBy = "operator", CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync();

        var accessor = new TestWorkspaceDatabaseAccessor(db);
        var user = new FixedAsterErpCurrentUser(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AsterErpClaimTypes.UserId, "operator"),
            new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
            new Claim(AsterErpClaimTypes.AppCode, "SYSTEM")
        }, "test")));
        var service = new ProjectManagementAuditService(accessor, user, new ProjectManagementAccessPolicy(accessor, user));

        var page = await service.QueryAsync(new ProjectManagementAuditQuery(ProjectId: "project-a", Keyword: "已更新"));
        Assert.Single(page.Items);
        var export = await service.ExportAsync(new ProjectManagementAuditQuery(ProjectId: "project-a"));
        var csv = System.Text.Encoding.UTF8.GetString(export.Content);
        Assert.Contains("\"标题, \"\"已更新\"\"\"", csv, StringComparison.Ordinal);
        Assert.Equal(1, export.Count);
    }

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
}

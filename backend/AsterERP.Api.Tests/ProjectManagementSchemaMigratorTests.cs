using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementSchemaMigratorTests
{
    [Fact]
    public async Task Migration_is_idempotent_and_creates_versioned_pm_schema()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-schema-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });

        var migrator = new ProjectManagementSchemaMigrator();
        await migrator.MigrateAsync(db, CancellationToken.None);
        await migrator.MigrateAsync(db, CancellationToken.None);

        Assert.True(db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name LIKE 'pm_%'") >= 15);
        Assert.Equal(2, db.Ado.GetInt("SELECT VersionNo FROM pm_schema_versions WHERE ModuleKey = 'project-management'"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'index' AND name = 'ux_pm_projects_code'"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'index' AND name = 'ux_pm_task_dependencies_pair'"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'index' AND name = 'ux_pm_tasks_sibling_sort_v2'"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'pm_task_attachments'"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = 'pm_im_conversation_links'"));
        Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(1) FROM sqlite_master WHERE type = 'index' AND name = 'ux_pm_im_conversation_link_scope'"));
    }

    [Fact]
    public async Task Attachment_schema_contains_file_and_object_scope_columns()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-attachment-schema-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });

        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        var tableInfo = db.Ado.SqlQuery<dynamic>("PRAGMA table_info(pm_task_attachments)");
        Assert.Contains(tableInfo, item => string.Equals((string)item.name, "TaskId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tableInfo, item => string.Equals((string)item.name, "FileId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tableInfo, item => string.Equals((string)item.name, "VersionNo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Schema_contains_workspace_and_concurrency_columns()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-schema-columns-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });

        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);

        var tableInfo = db.Ado.SqlQuery<dynamic>("PRAGMA table_info(pm_projects)");
        Assert.Contains(tableInfo, item => string.Equals((string)item.name, "TenantId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tableInfo, item => string.Equals((string)item.name, "AppCode", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tableInfo, item => string.Equals((string)item.name, "VersionNo", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Comment_mention_column_exists_for_new_and_legacy_schemas()
    {
        using var newDatabase = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-comment-schema-new-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        await new ProjectManagementSchemaMigrator().MigrateAsync(newDatabase, CancellationToken.None);
        Assert.Contains(newDatabase.Ado.SqlQuery<dynamic>("PRAGMA table_info(pm_task_comments)"), item => string.Equals((string)item.name, "MentionUserIdsJson", StringComparison.OrdinalIgnoreCase));

        using var legacyDatabase = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source=file:project-management-comment-schema-legacy-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false
        });
        legacyDatabase.Ado.ExecuteCommand("""
CREATE TABLE pm_task_comments (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL, ProjectId TEXT NOT NULL, TaskId TEXT NOT NULL,
    ParentCommentId TEXT NULL, Markdown TEXT NOT NULL, AuthorUserId TEXT NOT NULL, VersionNo INTEGER NOT NULL DEFAULT 1,
    EditedTime TEXT NULL, CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");

        await new ProjectManagementSchemaMigrator().MigrateAsync(legacyDatabase, CancellationToken.None);
        Assert.Contains(legacyDatabase.Ado.SqlQuery<dynamic>("PRAGMA table_info(pm_task_comments)"), item => string.Equals((string)item.name, "MentionUserIdsJson", StringComparison.OrdinalIgnoreCase));
    }
}

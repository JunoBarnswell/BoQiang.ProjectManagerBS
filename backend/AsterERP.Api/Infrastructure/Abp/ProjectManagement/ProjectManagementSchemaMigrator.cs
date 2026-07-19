using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.ProjectManagement;

public sealed class ProjectManagementSchemaMigrator
{
    public async Task MigrateAsync(ISqlSugarClient db, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var schema = new SqliteSchemaExecutor(db);
        CreateVersionTable(schema);
        CreateTables(schema);
        await BackfillTaskTreePathsAsync(db, cancellationToken);
        NormalizeTaskSiblingOrdering(schema);
        CreateIndexes(schema);
        SetVersion(schema);
    }

    private static void CreateVersionTable(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS pm_schema_versions (
    ModuleKey TEXT NOT NULL PRIMARY KEY,
    VersionNo INTEGER NOT NULL,
    AppliedAt TEXT NOT NULL,
    AppliedBy TEXT NULL
);
""");
    }

    private static void CreateTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS pm_projects (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    ProjectCode TEXT NOT NULL,
    ProjectName TEXT NOT NULL,
    Description TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Planning',
    Priority TEXT NOT NULL DEFAULT 'Medium',
    OwnerUserId TEXT NOT NULL,
    StartDate TEXT NULL,
    DueDate TEXT NULL,
    CompletedAt TEXT NULL,
    WipLimit INTEGER NULL,
    ProgressPercent REAL NOT NULL DEFAULT 0,
    VersionNo INTEGER NOT NULL DEFAULT 1,
    CreatedBy TEXT NULL,
    CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL,
    UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL,
    DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS pm_project_members (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    ProjectId TEXT NOT NULL,
    UserId TEXT NOT NULL,
    EmploymentId TEXT NULL,
    RoleCode TEXT NOT NULL DEFAULT 'Member',
    IsActive INTEGER NOT NULL DEFAULT 1,
    JoinedAt TEXT NOT NULL,
    LeftAt TEXT NULL,
    VersionNo INTEGER NOT NULL DEFAULT 1,
    CreatedBy TEXT NULL,
    CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL,
    UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL,
    DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS pm_milestones (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    ProjectId TEXT NOT NULL,
    MilestoneName TEXT NOT NULL,
    Description TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Planned',
    StartDate TEXT NULL,
    DueDate TEXT NULL,
    CompletedAt TEXT NULL,
    ProgressPercent REAL NOT NULL DEFAULT 0,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    VersionNo INTEGER NOT NULL DEFAULT 1,
    CreatedBy TEXT NULL,
    CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL,
    UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL,
    DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS pm_tasks (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    ProjectId TEXT NOT NULL,
    MilestoneId TEXT NULL,
    ParentTaskId TEXT NULL,
    TreePath TEXT NOT NULL DEFAULT '',
    TaskCode TEXT NOT NULL,
    Title TEXT NOT NULL,
    Description TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Todo',
    Priority TEXT NOT NULL DEFAULT 'Medium',
    AssigneeUserId TEXT NULL,
    AssigneeEmploymentId TEXT NULL,
    StartDate TEXT NULL,
    DueDate TEXT NULL,
    ActualStartAt TEXT NULL,
    ActualEndAt TEXT NULL,
    ProgressPercent REAL NOT NULL DEFAULT 0,
    Weight REAL NOT NULL DEFAULT 1,
    EstimateMinutes INTEGER NULL,
    ActualMinutes INTEGER NOT NULL DEFAULT 0,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    Depth INTEGER NOT NULL DEFAULT 0,
    VersionNo INTEGER NOT NULL DEFAULT 1,
    CreatedBy TEXT NULL,
    CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL,
    UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL,
    DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS pm_task_dependencies (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    ProjectId TEXT NOT NULL,
    PredecessorTaskId TEXT NOT NULL,
    SuccessorTaskId TEXT NOT NULL,
    DependencyType TEXT NOT NULL DEFAULT 'FinishToStart',
    LagMinutes INTEGER NOT NULL DEFAULT 0,
    VersionNo INTEGER NOT NULL DEFAULT 1,
    CreatedBy TEXT NULL,
    CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL,
    UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL,
    DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS pm_task_participants (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    ProjectId TEXT NOT NULL,
    TaskId TEXT NOT NULL,
    UserId TEXT NOT NULL,
    EmploymentId TEXT NULL,
    RoleCode TEXT NOT NULL DEFAULT 'Participant',
    VersionNo INTEGER NOT NULL DEFAULT 1,
    CreatedBy TEXT NULL,
    CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL,
    UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL,
    DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0,
    Remark TEXT NULL
);
""");
        schema.EnsureColumn("pm_project_members", "ScopeRootTaskId", "TEXT NULL");
        schema.EnsureColumn("pm_milestones", "OwnerUserId", "TEXT NULL");
        schema.EnsureColumn("pm_milestones", "HealthStatus", "TEXT NOT NULL DEFAULT 'OnTrack'");
        schema.EnsureColumn("pm_tasks", "BlockedReason", "TEXT NULL");
        schema.EnsureColumn("pm_tasks", "OccurrenceKey", "TEXT NULL");
        schema.EnsureColumn("pm_tasks", "TreePath", "TEXT NOT NULL DEFAULT ''");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS pm_labels (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    ProjectId TEXT NULL,
    LabelName TEXT NOT NULL,
    Color TEXT NOT NULL DEFAULT '#64748B',
    VersionNo INTEGER NOT NULL DEFAULT 1,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS pm_task_labels (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    ProjectId TEXT NOT NULL,
    TaskId TEXT NOT NULL,
    LabelId TEXT NOT NULL,
    VersionNo INTEGER NOT NULL DEFAULT 1,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS pm_task_time_logs (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    ProjectId TEXT NOT NULL,
    TaskId TEXT NOT NULL,
    UserId TEXT NOT NULL,
    StartedAt TEXT NOT NULL,
    EndedAt TEXT NOT NULL,
    Minutes INTEGER NOT NULL,
    Note TEXT NULL,
    VersionNo INTEGER NOT NULL DEFAULT 1,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS pm_task_templates (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL, ProjectId TEXT NULL,
    TemplateCode TEXT NOT NULL, TemplateName TEXT NOT NULL, DefinitionJson TEXT NOT NULL, RecurrenceExpression TEXT NULL,
    VersionNo INTEGER NOT NULL DEFAULT 1, CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
CREATE TABLE IF NOT EXISTS pm_task_occurrences (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL, TemplateId TEXT NOT NULL, ProjectId TEXT NOT NULL,
    OccurrenceKey TEXT NOT NULL, OccurrenceDate TEXT NOT NULL, RootTaskId TEXT NOT NULL, VersionNo INTEGER NOT NULL DEFAULT 1,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
CREATE TABLE IF NOT EXISTS pm_activities (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL, ProjectId TEXT NOT NULL,
    AggregateType TEXT NOT NULL, AggregateId TEXT NOT NULL, ActivityType TEXT NOT NULL, Summary TEXT NULL,
    TraceId TEXT NOT NULL, ActorUserId TEXT NOT NULL, CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL, UpdatedTime TEXT NULL, DeletedBy TEXT NULL, DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
CREATE TABLE IF NOT EXISTS pm_task_comments (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL, ProjectId TEXT NOT NULL, TaskId TEXT NOT NULL,
    ParentCommentId TEXT NULL, Markdown TEXT NOT NULL, MentionUserIdsJson TEXT NULL, AuthorUserId TEXT NOT NULL,
    VersionNo INTEGER NOT NULL DEFAULT 1, EditedTime TEXT NULL, CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL, UpdatedTime TEXT NULL, DeletedBy TEXT NULL, DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
CREATE TABLE IF NOT EXISTS pm_notifications (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL, RecipientUserId TEXT NOT NULL,
    NotificationType TEXT NOT NULL, Title TEXT NOT NULL, Message TEXT NOT NULL, TargetRoute TEXT NOT NULL, ProjectId TEXT NULL, TaskId TEXT NULL,
    TraceId TEXT NOT NULL, IdempotencyKey TEXT NOT NULL, IsRead INTEGER NOT NULL DEFAULT 0, ReadTime TEXT NULL,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
CREATE TABLE IF NOT EXISTS pm_task_reminders (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL, ProjectId TEXT NOT NULL, TaskId TEXT NOT NULL,
    RecipientUserId TEXT NOT NULL, ReminderAtUtc TEXT NOT NULL, TimeZoneId TEXT NOT NULL, Note TEXT NULL, Status TEXT NOT NULL,
    IdempotencyKey TEXT NOT NULL, HangfireJobId TEXT NULL, AttemptCount INTEGER NOT NULL DEFAULT 0, MaxAttempts INTEGER NOT NULL DEFAULT 3,
    LastAttemptAt TEXT NULL, TriggeredAt TEXT NULL, LastError TEXT NULL, VersionNo INTEGER NOT NULL DEFAULT 1,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
CREATE TABLE IF NOT EXISTS pm_saved_views (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL, ProjectId TEXT NOT NULL,
    ViewName TEXT NOT NULL, ViewKey TEXT NOT NULL, QueryJson TEXT NOT NULL, OwnerUserId TEXT NOT NULL,
    IsShared INTEGER NOT NULL DEFAULT 0, IsDefault INTEGER NOT NULL DEFAULT 0, VersionNo INTEGER NOT NULL DEFAULT 1,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
CREATE TABLE IF NOT EXISTS pm_task_attachments (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL, ProjectId TEXT NOT NULL, TaskId TEXT NOT NULL,
    FileId TEXT NOT NULL, FileName TEXT NOT NULL, ContentType TEXT NOT NULL, FileSize INTEGER NOT NULL, UploadedByUserId TEXT NOT NULL,
    VersionNo INTEGER NOT NULL DEFAULT 1, CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
CREATE TABLE IF NOT EXISTS pm_im_conversation_links (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL, ProjectId TEXT NOT NULL, TaskId TEXT NULL,
    ConversationKey TEXT NOT NULL, ConversationId TEXT NULL, MemberSource TEXT NOT NULL, Status TEXT NOT NULL, LastSyncError TEXT NULL,
    VersionNo INTEGER NOT NULL DEFAULT 1, CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
CREATE TABLE IF NOT EXISTS pm_sync_journal (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL, SequenceNo INTEGER NOT NULL,
    AggregateType TEXT NOT NULL, AggregateId TEXT NOT NULL, ProjectId TEXT NULL, Operation TEXT NOT NULL,
    VersionNo INTEGER NOT NULL, PayloadJson TEXT NOT NULL, ActorUserId TEXT NOT NULL, DeviceId TEXT NULL,
    TraceId TEXT NOT NULL, CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
CREATE TABLE IF NOT EXISTS pm_sync_devices (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL, DeviceId TEXT NOT NULL,
    LastExportedSequenceNo INTEGER NOT NULL DEFAULT 0, LastAcknowledgedSequenceNo INTEGER NOT NULL DEFAULT 0,
    LastSeenAt TEXT NOT NULL, CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
CREATE TABLE IF NOT EXISTS pm_maintenance_locks (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL, LockKey TEXT NOT NULL,
    OperationId TEXT NOT NULL, OwnerUserId TEXT NOT NULL, ExpiresAt TEXT NOT NULL,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
CREATE TABLE IF NOT EXISTS pm_backups (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL, BackupName TEXT NOT NULL,
    RelativePath TEXT NOT NULL, Sha256 TEXT NOT NULL, FileSize INTEGER NOT NULL, Status TEXT NOT NULL,
    CreatedByUserId TEXT NOT NULL, CompletedAt TEXT NULL, CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL,
    UpdatedBy TEXT NULL, UpdatedTime TEXT NULL, DeletedBy TEXT NULL, DeletedTime TEXT NULL,
    IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
CREATE TABLE IF NOT EXISTS pm_operations (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL, OperationType TEXT NOT NULL,
    Status TEXT NOT NULL, Phase TEXT NOT NULL DEFAULT 'Pending', ProgressPercent INTEGER NOT NULL DEFAULT 0, VersionNo INTEGER NOT NULL DEFAULT 1,
    IsCancellationRequested INTEGER NOT NULL DEFAULT 0, CancellationRequestedTime TEXT NULL, CancellationRequestedBy TEXT NULL,
    ImpactJson TEXT NOT NULL, ErrorMessage TEXT NULL, TraceId TEXT NOT NULL,
    ActorUserId TEXT NOT NULL, StartedTime TEXT NOT NULL, CompletedTime TEXT NULL,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
CREATE TABLE IF NOT EXISTS pm_operation_events (
    Id TEXT NOT NULL PRIMARY KEY, TenantId TEXT NOT NULL, AppCode TEXT NOT NULL, OperationId TEXT NOT NULL,
    Status TEXT NOT NULL, Phase TEXT NOT NULL, ProgressPercent INTEGER NOT NULL DEFAULT 0,
    IsCancellationRequested INTEGER NOT NULL DEFAULT 0, TraceId TEXT NOT NULL, ActorUserId TEXT NOT NULL,
    CreatedBy TEXT NULL, CreatedTime TEXT NOT NULL, UpdatedBy TEXT NULL, UpdatedTime TEXT NULL,
    DeletedBy TEXT NULL, DeletedTime TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0, Remark TEXT NULL
);
""");
        schema.EnsureColumn("pm_operations", "Phase", "TEXT NOT NULL DEFAULT 'Pending'");
        schema.EnsureColumn("pm_operations", "ProgressPercent", "INTEGER NOT NULL DEFAULT 0");
        schema.EnsureColumn("pm_operations", "VersionNo", "INTEGER NOT NULL DEFAULT 1");
        schema.EnsureColumn("pm_operations", "IsCancellationRequested", "INTEGER NOT NULL DEFAULT 0");
        schema.EnsureColumn("pm_operations", "CancellationRequestedTime", "TEXT NULL");
        schema.EnsureColumn("pm_operations", "CancellationRequestedBy", "TEXT NULL");
        schema.EnsureColumn("pm_notifications", "ProjectId", "TEXT NULL");
        schema.EnsureColumn("pm_notifications", "TaskId", "TEXT NULL");
        schema.EnsureColumn("pm_task_comments", "MentionUserIdsJson", "TEXT NULL");
    }

    private static void CreateIndexes(SqliteSchemaExecutor schema)
    {
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_projects_code ON pm_projects(TenantId, AppCode, ProjectCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_projects_workspace_status ON pm_projects(TenantId, AppCode, Status, IsDeleted);");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_projects_owner ON pm_projects(TenantId, AppCode, OwnerUserId, IsDeleted);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_project_members_user ON pm_project_members(TenantId, AppCode, ProjectId, UserId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_project_members_user ON pm_project_members(TenantId, AppCode, UserId, IsActive, IsDeleted);");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_milestones_project ON pm_milestones(TenantId, AppCode, ProjectId, SortOrder, IsDeleted);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_tasks_code ON pm_tasks(TenantId, AppCode, ProjectId, TaskCode) WHERE IsDeleted = 0;");
        schema.Execute("DROP INDEX IF EXISTS ux_pm_tasks_sibling_sort;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_tasks_sibling_sort_v2 ON pm_tasks(TenantId, AppCode, ProjectId, COALESCE(ParentTaskId, ''), CASE WHEN SortOrder = 0 THEN Id ELSE SortOrder END) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_tasks_tree ON pm_tasks(TenantId, AppCode, ProjectId, ParentTaskId, SortOrder, IsDeleted);");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_tasks_tree_path ON pm_tasks(TenantId, AppCode, ProjectId, TreePath, IsDeleted);");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_tasks_query ON pm_tasks(TenantId, AppCode, ProjectId, Status, AssigneeUserId, DueDate, IsDeleted);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_task_dependencies_pair ON pm_task_dependencies(TenantId, AppCode, ProjectId, PredecessorTaskId, SuccessorTaskId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_task_dependencies_successor ON pm_task_dependencies(TenantId, AppCode, SuccessorTaskId, IsDeleted);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_task_participants_user ON pm_task_participants(TenantId, AppCode, TaskId, UserId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_labels_project_name ON pm_labels(TenantId, AppCode, COALESCE(ProjectId, ''), LabelName) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_task_labels_pair ON pm_task_labels(TenantId, AppCode, TaskId, LabelId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_task_labels_project ON pm_task_labels(TenantId, AppCode, ProjectId, TaskId, IsDeleted);");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_task_time_logs_task ON pm_task_time_logs(TenantId, AppCode, ProjectId, TaskId, StartedAt, IsDeleted);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_task_templates_code ON pm_task_templates(TenantId, AppCode, COALESCE(ProjectId, ''), TemplateCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_task_occurrences_key ON pm_task_occurrences(TenantId, AppCode, TemplateId, ProjectId, OccurrenceKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_activities_project_time ON pm_activities(TenantId, AppCode, ProjectId, CreatedTime, IsDeleted);");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_task_comments_task_time ON pm_task_comments(TenantId, AppCode, ProjectId, TaskId, CreatedTime, IsDeleted);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_notifications_idempotency ON pm_notifications(TenantId, AppCode, IdempotencyKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_notifications_recipient ON pm_notifications(TenantId, AppCode, RecipientUserId, IsRead, CreatedTime, IsDeleted);");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_notifications_target ON pm_notifications(TenantId, AppCode, ProjectId, TaskId, IsDeleted);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_task_reminders_idempotency ON pm_task_reminders(TenantId, AppCode, IdempotencyKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_task_reminders_due ON pm_task_reminders(TenantId, AppCode, Status, ReminderAtUtc, IsDeleted);");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_task_reminders_task ON pm_task_reminders(TenantId, AppCode, ProjectId, TaskId, RecipientUserId, IsDeleted);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_saved_views_owner_name ON pm_saved_views(TenantId, AppCode, ProjectId, OwnerUserId, ViewName) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_saved_views_project ON pm_saved_views(TenantId, AppCode, ProjectId, IsShared, IsDefault, IsDeleted);");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_task_attachments_task ON pm_task_attachments(TenantId, AppCode, ProjectId, TaskId, CreatedTime, IsDeleted);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_im_conversation_link_scope ON pm_im_conversation_links(TenantId, AppCode, ProjectId, COALESCE(TaskId, '')) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_im_conversation_link_conversation ON pm_im_conversation_links(TenantId, AppCode, ConversationId) WHERE IsDeleted = 0 AND ConversationId IS NOT NULL;");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_im_conversation_link_project ON pm_im_conversation_links(TenantId, AppCode, ProjectId, Status, IsDeleted);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_sync_journal_sequence ON pm_sync_journal(TenantId, AppCode, SequenceNo);");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_sync_journal_project_sequence ON pm_sync_journal(TenantId, AppCode, ProjectId, SequenceNo, IsDeleted);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_sync_devices_device ON pm_sync_devices(TenantId, AppCode, DeviceId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS ux_pm_maintenance_locks_active ON pm_maintenance_locks(TenantId, AppCode, LockKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_maintenance_locks_expiry ON pm_maintenance_locks(TenantId, AppCode, ExpiresAt, IsDeleted);");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_backups_created ON pm_backups(TenantId, AppCode, CreatedTime, IsDeleted);");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_operations_status_time ON pm_operations(TenantId, AppCode, Status, StartedTime, IsDeleted);");
        schema.Execute("CREATE INDEX IF NOT EXISTS ix_pm_operation_events_operation_time ON pm_operation_events(TenantId, AppCode, OperationId, CreatedTime, IsDeleted);");
    }

    private static void NormalizeTaskSiblingOrdering(SqliteSchemaExecutor schema)
    {
        if (schema.HasIndex("ux_pm_tasks_sibling_sort_v2")) return;

        // Existing databases predate stable sibling ordering and may contain many
        // zero values. Materialize an unambiguous sparse order before the unique
        // index is created, preserving the prior order and deterministic ties.
        schema.Execute("""
WITH ordered AS (
    SELECT Id,
           ROW_NUMBER() OVER (
               PARTITION BY TenantId, AppCode, ProjectId, COALESCE(ParentTaskId, '')
               ORDER BY SortOrder, CreatedTime, Id
           ) * 1024 AS NextSortOrder
    FROM pm_tasks
    WHERE IsDeleted = 0
)
UPDATE pm_tasks
SET SortOrder = (SELECT NextSortOrder FROM ordered WHERE ordered.Id = pm_tasks.Id)
WHERE Id IN (SELECT Id FROM ordered);
""");
    }

    private static async Task BackfillTaskTreePathsAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        var tasks = await db.Queryable<ProjectManagementTaskEntity>()
            .ToListAsync(cancellationToken);
        if (tasks.Count == 0) return;
        var byId = tasks.ToDictionary(task => task.Id, StringComparer.Ordinal);
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        string Resolve(ProjectManagementTaskEntity task, HashSet<string> trail)
        {
            if (resolved.TryGetValue(task.Id, out var path)) return path;
            if (!trail.Add(task.Id)) return $"/{task.Id}/";
            if (!string.IsNullOrWhiteSpace(task.ParentTaskId) && byId.TryGetValue(task.ParentTaskId, out var parent) &&
                string.Equals(parent.ProjectId, task.ProjectId, StringComparison.Ordinal) &&
                string.Equals(parent.TenantId, task.TenantId, StringComparison.Ordinal) &&
                string.Equals(parent.AppCode, task.AppCode, StringComparison.Ordinal))
                path = Resolve(parent, trail) + task.Id + "/";
            else
                path = $"/{task.Id}/";
            trail.Remove(task.Id);
            resolved[task.Id] = path;
            return path;
        }
        var changed = new List<ProjectManagementTaskEntity>();
        foreach (var task in tasks)
        {
            var path = Resolve(task, []);
            if (string.Equals(task.TreePath, path, StringComparison.Ordinal)) continue;
            task.TreePath = path;
            changed.Add(task);
        }
        if (changed.Count > 0)
            await db.Updateable(changed).UpdateColumns(task => new { task.TreePath }).ExecuteCommandAsync(cancellationToken);
    }

    private static void SetVersion(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
INSERT INTO pm_schema_versions (ModuleKey, VersionNo, AppliedAt, AppliedBy)
VALUES ('project-management', 2, CURRENT_TIMESTAMP, 'schema-migrator')
ON CONFLICT(ModuleKey) DO UPDATE SET VersionNo = excluded.VersionNo, AppliedAt = excluded.AppliedAt, AppliedBy = excluded.AppliedBy;
""");
    }
}

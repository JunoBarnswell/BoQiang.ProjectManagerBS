using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.QueryViews;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.SystemAdministration;

public sealed class AsterErpSystemAdministrationSchemaMigrator
{
    public async Task MigrateAsync(IServiceProvider serviceProvider, ISqlSugarClient db, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var schema = new SqliteSchemaExecutor(db);
        CreateDictionaryTables(schema);
        CreateParameterTable(schema);
        CreateMessageSendLogTable(schema);
        CreateCodeRuleTables(schema);
        CreateFileRecordTable(schema);
        CreatePrintTables(schema);
        CreateAnnouncementTable(schema);
        CreateScheduledJobTables(schema);

        var queryViewMigrationService = serviceProvider.GetService<QueryViewMigrationService>();
        if (queryViewMigrationService is not null)
        {
            await queryViewMigrationService.MigrateAsync(cancellationToken);
        }
    }

    private static void CreateDictionaryTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_dict_types (
    Id TEXT NOT NULL PRIMARY KEY,
    DictName TEXT NOT NULL,
    DictCode TEXT NOT NULL,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
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
CREATE TABLE IF NOT EXISTS system_dict_items (
    Id TEXT NOT NULL PRIMARY KEY,
    DictTypeId TEXT NOT NULL,
    ItemLabel TEXT NOT NULL,
    ItemValue TEXT NOT NULL,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
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

        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_dict_types_code ON system_dict_types(DictCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_dict_items_type_order ON system_dict_items(DictTypeId, SortOrder);");
    }

    private static void CreateParameterTable(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_parameters (
    Id TEXT NOT NULL PRIMARY KEY,
    ParamName TEXT NOT NULL,
    ParamKey TEXT NOT NULL,
    ParamValue TEXT NOT NULL,
    Category TEXT NOT NULL DEFAULT 'general',
    IsEnabled INTEGER NOT NULL DEFAULT 1,
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

        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_parameters_key ON system_parameters(ParamKey) WHERE IsDeleted = 0;");
        schema.CreateIndexIfColumnsExist("system_parameters", "idx_system_parameters_list_created", "IsDeleted", "CreatedTime");
        schema.CreateIndexIfColumnsExist("system_parameters", "idx_system_parameters_category_enabled", "IsDeleted", "Category", "IsEnabled");
    }

    private static void CreateMessageSendLogTable(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_message_send_logs (
    Id TEXT NOT NULL PRIMARY KEY,
    Channel TEXT NOT NULL,
    Provider TEXT NOT NULL,
    MaskedTarget TEXT NULL,
    TraceId TEXT NOT NULL,
    CorrelationId TEXT NULL,
    Result TEXT NOT NULL,
    ErrorSummary TEXT NULL,
    DurationMs INTEGER NOT NULL DEFAULT 0,
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

        schema.CreateIndexIfColumnsExist("system_message_send_logs", "idx_system_message_send_logs_list_created", "IsDeleted", "CreatedTime");
        schema.CreateIndexIfColumnsExist("system_message_send_logs", "idx_system_message_send_logs_channel_result", "IsDeleted", "Channel", "Result");
        schema.CreateIndexIfColumnsExist("system_message_send_logs", "idx_system_message_send_logs_trace", "TraceId");
    }

    private static void CreateCodeRuleTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_code_rules (
    Id TEXT NOT NULL PRIMARY KEY,
    RuleName TEXT NOT NULL,
    RuleCode TEXT NOT NULL,
    ResetPolicy TEXT NOT NULL DEFAULT 'Daily',
    CurrentDateKey TEXT NULL,
    CurrentSequence INTEGER NOT NULL DEFAULT 0,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
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
CREATE TABLE IF NOT EXISTS system_code_rule_segments (
    Id TEXT NOT NULL PRIMARY KEY,
    CodeRuleId TEXT NOT NULL,
    SegmentType TEXT NOT NULL DEFAULT 'Static',
    SegmentValue TEXT NULL,
    SegmentLength INTEGER NOT NULL DEFAULT 0,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
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

        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_code_rules_code ON system_code_rules(RuleCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_code_rule_segments_rule_order ON system_code_rule_segments(CodeRuleId, SortOrder);");
    }

    private static void CreateFileRecordTable(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_file_records (
    Id TEXT NOT NULL PRIMARY KEY,
    FileName TEXT NOT NULL,
    ContentType TEXT NOT NULL,
    FileSize INTEGER NOT NULL DEFAULT 0,
    StoredPath TEXT NOT NULL,
    Sha256 TEXT NOT NULL,
    BusinessType TEXT NULL,
    BusinessId TEXT NULL,
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

        schema.EnsureColumn("system_file_records", "StoredPath", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("system_file_records", "FileSize", "INTEGER NOT NULL DEFAULT 0");
        schema.EnsureColumn("system_file_records", "BusinessType", "TEXT NULL");
        schema.EnsureColumn("system_file_records", "BusinessId", "TEXT NULL");
        schema.CreateIndexIfColumnsExist("system_file_records", "idx_system_file_records_business", "BusinessType", "BusinessId");
        schema.CreateIndexIfColumnsExist("system_file_records", "idx_system_file_records_sha256", "Sha256");
    }

    private static void CreatePrintTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_print_templates (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    MenuCode TEXT NOT NULL,
    Scene TEXT NOT NULL,
    TemplateCode TEXT NOT NULL,
    Name TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Draft',
    IsDefault INTEGER NOT NULL DEFAULT 0,
    DraftDataJson TEXT NULL,
    DraftExtJson TEXT NULL,
    DraftPermissionsJson TEXT NULL,
    PublishedDataJson TEXT NULL,
    PublishedExtJson TEXT NULL,
    PublishedPermissionsJson TEXT NULL,
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
CREATE TABLE IF NOT EXISTS system_print_custom_elements (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    Name TEXT NOT NULL,
    ElementJson TEXT NULL,
    ExtJson TEXT NULL,
    PermissionsJson TEXT NULL,
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

        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_print_templates_scope_code ON system_print_templates(TenantId, AppCode, MenuCode, Scene, TemplateCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_print_templates_scope_default ON system_print_templates(TenantId, AppCode, MenuCode, Scene, IsDefault) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_print_custom_elements_scope_name ON system_print_custom_elements(TenantId, AppCode, Name) WHERE IsDeleted = 0;");
    }

    private static void CreateAnnouncementTable(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_announcements (
    Id TEXT NOT NULL PRIMARY KEY,
    Title TEXT NOT NULL,
    Content TEXT NOT NULL,
    AnnouncementType TEXT NOT NULL,
    Scope TEXT NOT NULL,
    Priority INTEGER NOT NULL DEFAULT 0,
    Status TEXT NOT NULL,
    IsPinned INTEGER NOT NULL DEFAULT 0,
    PublishedAt TEXT NULL,
    RevokedAt TEXT NULL,
    ExpiresAt TEXT NULL,
    PublishedBy TEXT NULL,
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

        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_announcements_status ON system_announcements(Status);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_announcements_published ON system_announcements(PublishedAt);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_announcements_type ON system_announcements(AnnouncementType);");
        schema.CreateIndexIfColumnsExist("system_announcements", "idx_system_announcements_list_default", "IsDeleted", "Status", "IsPinned", "PublishedAt", "CreatedTime");
        schema.CreateIndexIfColumnsExist("system_announcements", "idx_system_announcements_priority", "IsDeleted", "Priority");
    }

    private static void CreateScheduledJobTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_scheduled_jobs (
    Id TEXT NOT NULL PRIMARY KEY,
    JobName TEXT NOT NULL,
    JobCode TEXT NOT NULL,
    JobType TEXT NOT NULL,
    PresetJobCode TEXT NULL,
    Status TEXT NOT NULL,
    ScheduleKind TEXT NOT NULL,
    IntervalValue INTEGER NULL,
    TimeOfDay TEXT NULL,
    WeekDaysJson TEXT NULL,
    MonthDaysJson TEXT NULL,
    TimeZoneId TEXT NOT NULL,
    ScheduleConfigJson TEXT NOT NULL,
    ParameterJson TEXT NULL,
    HttpCallbackJson TEXT NULL,
    CronExpression TEXT NOT NULL,
    FriendlySchedule TEXT NOT NULL,
    ScheduleSyncStatus TEXT NOT NULL,
    LastSyncError TEXT NULL,
    LastResult TEXT NULL,
    LastRunAt TEXT NULL,
    NextRunAt TEXT NULL,
    LastErrorMessage TEXT NULL,
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
CREATE TABLE IF NOT EXISTS system_scheduled_job_logs (
    Id TEXT NOT NULL PRIMARY KEY,
    ScheduledJobId TEXT NOT NULL,
    HangfireJobId TEXT NULL,
    TriggerType TEXT NOT NULL,
    Result TEXT NOT NULL,
    StartedAt TEXT NOT NULL,
    FinishedAt TEXT NULL,
    DurationMs INTEGER NOT NULL DEFAULT 0,
    ErrorMessage TEXT NULL,
    OutputSummary TEXT NULL,
    TraceId TEXT NOT NULL,
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

        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_scheduled_jobs_code ON system_scheduled_jobs(JobCode);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_scheduled_jobs_status ON system_scheduled_jobs(Status);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_scheduled_jobs_type ON system_scheduled_jobs(JobType);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_scheduled_jobs_next_run ON system_scheduled_jobs(NextRunAt);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_scheduled_job_logs_job_time ON system_scheduled_job_logs(ScheduledJobId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_scheduled_job_logs_result_time ON system_scheduled_job_logs(Result, CreatedTime);");
        schema.CreateIndexIfColumnsExist("system_scheduled_jobs", "idx_system_scheduled_jobs_list_created", "IsDeleted", "CreatedTime");
        schema.CreateIndexIfColumnsExist("system_scheduled_jobs", "idx_system_scheduled_jobs_status_type", "IsDeleted", "Status", "JobType");
        schema.CreateIndexIfColumnsExist("system_scheduled_job_logs", "idx_system_scheduled_job_logs_list_created", "IsDeleted", "CreatedTime");
    }
}

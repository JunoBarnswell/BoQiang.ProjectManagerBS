using AsterERP.Api.Infrastructure.Database;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

internal static class AiCenterManagementSchemaMigrator
{
    public static void Migrate(SqliteSchemaExecutor schema)
    {
        CreateAiCenterManagementTables(schema);
    }

    private static void CreateAiCenterManagementTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS ai_system_settings (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    SettingKey TEXT NOT NULL,
    SettingValue TEXT NOT NULL,
    ValueType TEXT NULL,
    Description TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_prompt_versions (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    PromptTemplateId TEXT NOT NULL,
    VersionNo INTEGER NOT NULL,
    SystemPrompt TEXT NOT NULL,
    UserPromptTemplate TEXT NULL,
    VariablesJson TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Draft',
    ChangeSummary TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_tool_definitions (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    ToolCode TEXT NOT NULL,
    ToolName TEXT NOT NULL,
    ToolType TEXT NOT NULL DEFAULT 'Api',
    ToolDomain TEXT NOT NULL DEFAULT '',
    RiskLevel TEXT NOT NULL DEFAULT 'low',
    RequiresConfirmation INTEGER NOT NULL DEFAULT 0,
    PermissionCode TEXT NOT NULL DEFAULT '',
    InputSchemaJson TEXT NOT NULL DEFAULT '{}',
    OutputSchemaJson TEXT NOT NULL DEFAULT '{}',
    Status TEXT NOT NULL DEFAULT 'Enabled',
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
CREATE TABLE IF NOT EXISTS ai_tool_bindings (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    AgentProfileId TEXT NOT NULL,
    ToolCode TEXT NOT NULL,
    AutoInvokeAllowed INTEGER NOT NULL DEFAULT 0,
    Status TEXT NOT NULL DEFAULT 'Enabled',
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
CREATE TABLE IF NOT EXISTS ai_workflow_tool_bindings (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    WorkflowModelId TEXT NOT NULL,
    WorkflowCode TEXT NOT NULL,
    WorkflowName TEXT NOT NULL,
    ToolCode TEXT NOT NULL,
    RiskLevel TEXT NOT NULL DEFAULT 'high',
    RequiresConfirmation INTEGER NOT NULL DEFAULT 1,
    Status TEXT NOT NULL DEFAULT 'Enabled',
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
CREATE TABLE IF NOT EXISTS ai_knowledge_index_tasks (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    SourceId TEXT NULL,
    DocumentId TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Pending',
    Progress INTEGER NOT NULL DEFAULT 0,
    ErrorMessage TEXT NULL,
    StartedTime TEXT NULL,
    FinishedTime TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_secret_refs (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    SecretCode TEXT NOT NULL,
    SecretType TEXT NOT NULL DEFAULT 'ApiKey',
    CipherText TEXT NOT NULL,
    Mask TEXT NOT NULL,
    ResourceType TEXT NULL,
    ResourceId TEXT NULL,
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
    }


}



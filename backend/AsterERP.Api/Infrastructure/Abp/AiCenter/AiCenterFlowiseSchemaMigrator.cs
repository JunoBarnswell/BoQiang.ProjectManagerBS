using AsterERP.Api.Infrastructure.Database;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

internal static class AiCenterFlowiseSchemaMigrator
{
    public static void Migrate(SqliteSchemaExecutor schema)
    {
        CreateFlowiseStudioTables(schema);
    }

    private static void CreateFlowiseStudioTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS ai_flowise_workspaces (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceKey TEXT NOT NULL,
    WorkspaceName TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Enabled',
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
CREATE TABLE IF NOT EXISTS ai_flowise_chat_flows (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    Name TEXT NOT NULL,
    FlowData TEXT NOT NULL DEFAULT '{}',
    Type TEXT NOT NULL DEFAULT 'CHATFLOW',
    Deployed INTEGER NOT NULL DEFAULT 0,
    IsPublic INTEGER NOT NULL DEFAULT 0,
    Apikeyid TEXT NULL,
    Category TEXT NULL,
    MetadataJson TEXT NOT NULL DEFAULT '{}',
    ChatbotConfig TEXT NOT NULL DEFAULT '{}',
    ApiConfig TEXT NOT NULL DEFAULT '{}',
    Analytic TEXT NOT NULL DEFAULT '{}',
    SpeechToText TEXT NOT NULL DEFAULT '{}',
    TextToSpeech TEXT NOT NULL DEFAULT '{}',
    FollowUpPrompts TEXT NOT NULL DEFAULT '{}',
    McpServerConfig TEXT NOT NULL DEFAULT '{}',
    WebhookSecretCipherText TEXT NULL,
    WebhookSecretConfigured INTEGER NOT NULL DEFAULT 0,
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
CREATE TABLE IF NOT EXISTS ai_flowise_sso_configs (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    ConfigKey TEXT NOT NULL,
    Provider TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Enabled',
    Enabled INTEGER NOT NULL DEFAULT 0,
    SettingsJson TEXT NOT NULL DEFAULT '{}',
    MetadataJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_roles (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    RoleKey TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Enabled',
    PermissionsJson TEXT NOT NULL DEFAULT '[]',
    MetadataJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_users (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    UserKey TEXT NOT NULL,
    Name TEXT NOT NULL,
    Email TEXT NULL,
    Description TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Enabled',
    RolesJson TEXT NOT NULL DEFAULT '[]',
    WorkspaceIdsJson TEXT NOT NULL DEFAULT '[]',
    MetadataJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_login_activity (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    UserName TEXT NOT NULL,
    IpAddress TEXT NULL,
    UserAgent TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Success',
    DetailJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_account_settings (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    DisplayName TEXT NOT NULL,
    Email TEXT NULL,
    PreferencesJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_tools (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    ToolKey TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    ToolType TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Enabled',
    SchemaJson TEXT NOT NULL DEFAULT '{}',
    ImplementationJson TEXT NOT NULL DEFAULT '{}',
    MetadataJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_custom_mcp_servers (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    Name TEXT NOT NULL,
    ServerUrl TEXT NOT NULL,
    IconSrc TEXT NULL,
    Color TEXT NULL,
    AuthType TEXT NOT NULL DEFAULT 'none',
    AuthConfigCipherText TEXT NULL,
    AuthConfigMaskJson TEXT NOT NULL DEFAULT '{}',
    ToolsJson TEXT NOT NULL DEFAULT '[]',
    ToolCount INTEGER NOT NULL DEFAULT 0,
    Status TEXT NOT NULL DEFAULT 'Enabled',
    ErrorMessage TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_flowise_credentials (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    CredentialKey TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    CredentialType TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Enabled',
    ConfigJson TEXT NOT NULL DEFAULT '{}',
    MetadataJson TEXT NOT NULL DEFAULT '{}',
    SecretCipherText TEXT NULL,
    SecretHash TEXT NULL,
    SecretMask TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_flowise_variables (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    VariableKey TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    Scope TEXT NULL,
    IsSecret INTEGER NOT NULL DEFAULT 0,
    Status TEXT NOT NULL DEFAULT 'Enabled',
    ValueJson TEXT NOT NULL DEFAULT '{}',
    MetadataJson TEXT NOT NULL DEFAULT '{}',
    SecretCipherText TEXT NULL,
    SecretHash TEXT NULL,
    SecretMask TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_flowise_api_keys (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    ApiKeyCode TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Enabled',
    KeyHash TEXT NOT NULL,
    KeyMask TEXT NOT NULL,
    MetadataJson TEXT NOT NULL DEFAULT '{}',
    LastUsedAt TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_flowise_assistants (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    AssistantKey TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    AssistantType TEXT NOT NULL DEFAULT 'custom',
    Status TEXT NOT NULL DEFAULT 'Enabled',
    DefinitionJson TEXT NOT NULL DEFAULT '{}',
    MetadataJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_marketplace_templates (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    TemplateKey TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    Category TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Enabled',
    FlowData TEXT NOT NULL DEFAULT '{}',
    MetadataJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_document_stores (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    StoreKey TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    Category TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Enabled',
    LoaderConfigJson TEXT NOT NULL DEFAULT '{}',
    MetadataJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_datasets (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    DatasetKey TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    Category TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Enabled',
    SchemaJson TEXT NOT NULL DEFAULT '{}',
    MetadataJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_evaluators (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    EvaluatorKey TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    EvaluatorType TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Enabled',
    DefinitionJson TEXT NOT NULL DEFAULT '{}',
    MetadataJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_evaluations (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    EvaluationKey TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    Category TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Enabled',
    DefinitionJson TEXT NOT NULL DEFAULT '{}',
    MetadataJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_executions (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    ResourceId TEXT NOT NULL,
    ResourceName TEXT NOT NULL,
    FlowType TEXT NOT NULL DEFAULT 'Chatflow',
    Status TEXT NOT NULL DEFAULT 'Queued',
    InputJson TEXT NOT NULL DEFAULT '{}',
    OutputJson TEXT NOT NULL DEFAULT '{}',
    SourceDocumentsJson TEXT NOT NULL DEFAULT '[]',
    ActionJson TEXT NULL,
    ErrorCode TEXT NULL,
    ErrorMessage TEXT NULL,
    TraceId TEXT NOT NULL,
    DurationMs INTEGER NOT NULL DEFAULT 0,
    StartedAt TEXT NULL,
    CompletedAt TEXT NULL,
    IdempotencyKey TEXT NULL,
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
        schema.EnsureColumn("ai_flowise_executions", "SourceDocumentsJson", "TEXT NOT NULL DEFAULT '[]'");
        schema.EnsureColumn("ai_flowise_executions", "ActionJson", "TEXT NULL");

        schema.Execute("""
CREATE TABLE IF NOT EXISTS ai_flowise_schedule_records (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    TriggerType TEXT NOT NULL DEFAULT 'AGENTFLOW',
    TargetId TEXT NOT NULL,
    NodeId TEXT NULL,
    CronExpression TEXT NOT NULL,
    Timezone TEXT NOT NULL DEFAULT 'UTC',
    Enabled INTEGER NOT NULL DEFAULT 1,
    ScheduleInputMode TEXT NOT NULL DEFAULT 'text',
    DefaultInput TEXT NULL,
    DefaultForm TEXT NULL,
    LastRunAt TEXT NULL,
    NextRunAt TEXT NULL,
    EndDate TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_flowise_schedule_trigger_logs (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    ScheduleRecordId TEXT NOT NULL,
    TriggerType TEXT NOT NULL DEFAULT 'AGENTFLOW',
    TargetId TEXT NOT NULL,
    ExecutionId TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'QUEUED',
    Error TEXT NULL,
    ElapsedTimeMs INTEGER NULL,
    ScheduledAt TEXT NOT NULL,
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
CREATE TABLE IF NOT EXISTS ai_flowise_audit_logs (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    EventType TEXT NOT NULL,
    ResourceType TEXT NOT NULL,
    ResourceId TEXT NULL,
    DetailJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_node_definitions (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    NodeType TEXT NOT NULL,
    Label TEXT NOT NULL,
    Category TEXT NOT NULL,
    Description TEXT NULL,
    Icon TEXT NULL,
    Version INTEGER NOT NULL DEFAULT 1,
    DefinitionJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_shared_workspaces (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    ItemId TEXT NOT NULL,
    ItemType TEXT NOT NULL,
    SharedWorkspaceId TEXT NOT NULL,
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
CREATE TABLE IF NOT EXISTS ai_flowise_chat_messages (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    ResourceId TEXT NOT NULL,
    ExecutionId TEXT NULL,
    ChatId TEXT NULL,
    Role TEXT NOT NULL,
    Message TEXT NOT NULL,
    SourceDocumentsJson TEXT NOT NULL DEFAULT '[]',
    FileUploadsJson TEXT NOT NULL DEFAULT '[]',
    AgentReasoningJson TEXT NOT NULL DEFAULT '[]',
    AgentExecutedDataJson TEXT NOT NULL DEFAULT '[]',
    UsedToolsJson TEXT NOT NULL DEFAULT '[]',
    ArtifactsJson TEXT NOT NULL DEFAULT '[]',
    ActionJson TEXT NULL,
    FollowUpPromptsJson TEXT NULL,
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

        schema.EnsureColumn("ai_flowise_chat_messages", "FileUploadsJson", "TEXT NOT NULL DEFAULT '[]'");
        schema.EnsureColumn("ai_flowise_chat_messages", "AgentReasoningJson", "TEXT NOT NULL DEFAULT '[]'");
        schema.EnsureColumn("ai_flowise_chat_messages", "AgentExecutedDataJson", "TEXT NOT NULL DEFAULT '[]'");
        schema.EnsureColumn("ai_flowise_chat_messages", "UsedToolsJson", "TEXT NOT NULL DEFAULT '[]'");
        schema.EnsureColumn("ai_flowise_chat_messages", "ArtifactsJson", "TEXT NOT NULL DEFAULT '[]'");
        schema.EnsureColumn("ai_flowise_chat_messages", "ActionJson", "TEXT NULL");
        schema.EnsureColumn("ai_flowise_chat_messages", "FollowUpPromptsJson", "TEXT NULL");
        schema.EnsureColumn("ai_flowise_chat_flows", "MetadataJson", "TEXT NOT NULL DEFAULT '{}'");

        schema.Execute("""
CREATE TABLE IF NOT EXISTS ai_flowise_feedback (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    MessageId TEXT NOT NULL,
    Rating TEXT NOT NULL,
    Reason TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_flowise_leads (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    ResourceId TEXT NOT NULL,
    ContactJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_document_store_files (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    StoreId TEXT NOT NULL,
    FileName TEXT NOT NULL,
    FileSize INTEGER NOT NULL DEFAULT 0,
    LoaderType TEXT NOT NULL,
    LoaderConfigJson TEXT NOT NULL DEFAULT '{}',
    Status TEXT NOT NULL DEFAULT 'Pending',
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
CREATE TABLE IF NOT EXISTS ai_flowise_document_store_chunks (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    StoreId TEXT NOT NULL,
    DocumentId TEXT NULL,
    ChunkIndex INTEGER NOT NULL DEFAULT 0,
    Content TEXT NOT NULL,
    MetadataJson TEXT NOT NULL DEFAULT '{}',
    TokenCount INTEGER NOT NULL DEFAULT 0,
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
CREATE TABLE IF NOT EXISTS ai_flowise_vector_store_configs (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    StoreId TEXT NOT NULL,
    VectorProvider TEXT NOT NULL,
    EmbeddingProvider TEXT NOT NULL,
    RecordManagerProvider TEXT NULL,
    VectorStoreConfigJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_document_store_upsert_history (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    StoreId TEXT NOT NULL,
    LoaderId TEXT NULL,
    ChatflowId TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Completed',
    ProcessedCount INTEGER NOT NULL DEFAULT 0,
    AddedCount INTEGER NOT NULL DEFAULT 0,
    ReplacedCount INTEGER NOT NULL DEFAULT 0,
    SkippedCount INTEGER NOT NULL DEFAULT 0,
    ErrorMessage TEXT NULL,
    RequestJson TEXT NOT NULL DEFAULT '{}',
    ResultJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_dataset_rows (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    DatasetId TEXT NOT NULL,
    Input TEXT NOT NULL,
    ExpectedOutput TEXT NULL,
    ActualOutput TEXT NULL,
    MetadataJson TEXT NOT NULL DEFAULT '{}',
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
CREATE TABLE IF NOT EXISTS ai_flowise_evaluation_results (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    WorkspaceId TEXT NULL,
    EvaluationId TEXT NOT NULL,
    VersionNo INTEGER NOT NULL DEFAULT 1,
    Status TEXT NOT NULL DEFAULT 'Pending',
    PassRate NUMERIC NOT NULL DEFAULT 0,
    AverageLatencyMs INTEGER NOT NULL DEFAULT 0,
    TotalTokens INTEGER NOT NULL DEFAULT 0,
    MetricsJson TEXT NOT NULL DEFAULT '{}',
    ResultRowsJson TEXT NOT NULL DEFAULT '[]',
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



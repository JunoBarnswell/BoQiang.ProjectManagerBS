using AsterERP.Api.Infrastructure.Database;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

internal static class AiCenterCoreSchemaMigrator
{
    public static void Migrate(SqliteSchemaExecutor schema)
    {
        CreateProviderTables(schema);
        CreateConversationTables(schema);
        CreateGovernanceTables(schema);
        CreateWorkflowToolTables(schema);
        CreateSkCapabilityTables(schema);
        CreateKnowledgeTables(schema);
        CreateTaskProcessTables(schema);
    }

    private static void CreateProviderTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS ai_providers (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    ProviderCode TEXT NOT NULL,
    ProviderName TEXT NOT NULL,
    ProtocolType TEXT NOT NULL DEFAULT 'OpenAiCompatible',
    BaseUrl TEXT NOT NULL,
    ApiKeyCipherText TEXT NULL,
    ApiKeyMask TEXT NULL,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    TimeoutSeconds INTEGER NOT NULL DEFAULT 120,
    ExtraParametersJson TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_model_configs (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    ProviderId TEXT NOT NULL,
    ModelCode TEXT NOT NULL,
    DisplayName TEXT NOT NULL,
    MaxContextTokens INTEGER NOT NULL DEFAULT 64000,
    MaxOutputTokens INTEGER NOT NULL DEFAULT 8192,
    DefaultTemperature NUMERIC NULL,
    DefaultTopP NUMERIC NULL,
    ThinkingEnabledDefault INTEGER NOT NULL DEFAULT 1,
    ReasoningEffort TEXT NULL,
    ToolStreamEnabledDefault INTEGER NOT NULL DEFAULT 0,
    MaxParallelRuns INTEGER NOT NULL DEFAULT 3,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    SortOrder INTEGER NOT NULL DEFAULT 0,
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


    private static void CreateConversationTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS ai_conversations (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    Title TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Active',
    IsFavorite INTEGER NOT NULL DEFAULT 0,
    Summary TEXT NULL,
    ActiveSnapshotId TEXT NULL,
    LastRunStatus TEXT NULL,
    LastMessageAt TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_messages (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    ConversationId TEXT NOT NULL,
    RunId TEXT NULL,
    ParentMessageId TEXT NULL,
    AgentProfileId TEXT NULL,
    Role TEXT NOT NULL,
    Seq INTEGER NOT NULL,
    Content TEXT NOT NULL,
    ReasoningContent TEXT NULL,
    ToolCallsJson TEXT NULL,
    TokenCount INTEGER NOT NULL DEFAULT 0,
    FinishReason TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Completed',
    MetadataJson TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_chat_runs (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    ConversationId TEXT NOT NULL,
    UserMessageId TEXT NULL,
    AssistantMessageId TEXT NULL,
    ProviderId TEXT NULL,
    ModelConfigId TEXT NULL,
    Mode TEXT NOT NULL DEFAULT 'Single',
    Status TEXT NOT NULL DEFAULT 'Queued',
    ClientMessageId TEXT NULL,
    IdempotencyKey TEXT NULL,
    RequestHash TEXT NULL,
    AgentProfileIdsJson TEXT NULL,
    CoordinatorAgentProfileId TEXT NULL,
    PromptTokens INTEGER NOT NULL DEFAULT 0,
    CompletionTokens INTEGER NOT NULL DEFAULT 0,
    ReasoningTokens INTEGER NOT NULL DEFAULT 0,
    TotalTokens INTEGER NOT NULL DEFAULT 0,
    ErrorCode TEXT NULL,
    ErrorMessage TEXT NULL,
    TraceId TEXT NULL,
    StartedAt TEXT NULL,
    CompletedAt TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_run_participants (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    RunId TEXT NOT NULL,
    AgentProfileId TEXT NOT NULL,
    AgentName TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Queued',
    DraftMessageId TEXT NULL,
    PromptTokens INTEGER NOT NULL DEFAULT 0,
    CompletionTokens INTEGER NOT NULL DEFAULT 0,
    ReasoningTokens INTEGER NOT NULL DEFAULT 0,
    TotalTokens INTEGER NOT NULL DEFAULT 0,
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
CREATE TABLE IF NOT EXISTS ai_context_snapshots (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    ConversationId TEXT NOT NULL,
    FromSeq INTEGER NOT NULL,
    ToSeq INTEGER NOT NULL,
    Summary TEXT NOT NULL,
    ModelConfigId TEXT NULL,
    PromptTokens INTEGER NOT NULL DEFAULT 0,
    CompletionTokens INTEGER NOT NULL DEFAULT 0,
    TotalTokens INTEGER NOT NULL DEFAULT 0,
    SnapshotType TEXT NOT NULL DEFAULT 'Manual',
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
CREATE TABLE IF NOT EXISTS ai_prompt_templates (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    TemplateCode TEXT NOT NULL,
    TemplateName TEXT NOT NULL,
    Category TEXT NOT NULL DEFAULT 'general',
    SystemPrompt TEXT NOT NULL,
    UserPromptTemplate TEXT NULL,
    VariablesJson TEXT NULL,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    SortOrder INTEGER NOT NULL DEFAULT 0,
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
CREATE TABLE IF NOT EXISTS ai_agent_profiles (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    AgentCode TEXT NOT NULL,
    AgentName TEXT NOT NULL,
    RolePrompt TEXT NOT NULL,
    ModelConfigId TEXT NULL,
    PromptTemplateId TEXT NULL,
    AllowedFunctionsJson TEXT NULL,
    IsCoordinator INTEGER NOT NULL DEFAULT 0,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    SortOrder INTEGER NOT NULL DEFAULT 0,
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

        schema.EnsureColumn("ai_agent_profiles", "AllowedFunctionsJson", "TEXT NULL");
        if (schema.HasColumn("ai_agent_profiles", "ToolsJson"))
        {
            schema.Execute("""
UPDATE ai_agent_profiles
SET AllowedFunctionsJson = ToolsJson
WHERE AllowedFunctionsJson IS NULL
  AND ToolsJson IS NOT NULL;
""");
        }

        schema.Execute("""
CREATE TABLE IF NOT EXISTS ai_task_plans (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    ConversationId TEXT NOT NULL,
    RunId TEXT NULL,
    Title TEXT NOT NULL,
    Goal TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Draft',
    Mode TEXT NOT NULL DEFAULT 'Plan',
    VersionNo INTEGER NOT NULL DEFAULT 1,
    Revision INTEGER NOT NULL DEFAULT 0,
    ExecutionStrategy TEXT NOT NULL DEFAULT 'Serial',
    RisksJson TEXT NULL,
    AssumptionsJson TEXT NULL,
    MetadataJson TEXT NULL,
    ApprovedBy TEXT NULL,
    ApprovedRevision INTEGER NULL,
    ApprovedAt TEXT NULL,
    CompletedAt TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_task_plan_items (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    ConversationId TEXT NOT NULL,
    PlanId TEXT NOT NULL,
    ParentItemId TEXT NULL,
    Title TEXT NOT NULL,
    Description TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Pending',
    Priority TEXT NOT NULL DEFAULT 'P1',
    OwnerType TEXT NOT NULL DEFAULT 'Agent',
    TaskType TEXT NOT NULL DEFAULT 'Design',
    SortOrder INTEGER NOT NULL DEFAULT 0,
    Depth INTEGER NOT NULL DEFAULT 0,
    DependsOnJson TEXT NULL,
    AcceptanceCriteriaJson TEXT NULL,
    ToolCode TEXT NULL,
    ExecutionHint TEXT NULL,
    Result TEXT NULL,
    ResultSummary TEXT NULL,
    EvidenceJson TEXT NULL,
    ErrorCode TEXT NULL,
    ErrorMessage TEXT NULL,
    BlockedReason TEXT NULL,
    SkipReason TEXT NULL,
    RetryCount INTEGER NOT NULL DEFAULT 0,
    MaxRetryCount INTEGER NOT NULL DEFAULT 3,
    StartedAt TEXT NULL,
    CompletedAt TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_task_plan_events (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    ConversationId TEXT NOT NULL,
    PlanId TEXT NOT NULL,
    ItemId TEXT NULL,
    RunId TEXT NULL,
    Seq INTEGER NOT NULL DEFAULT 0,
    EventName TEXT NOT NULL,
    FromStatus TEXT NULL,
    ToStatus TEXT NULL,
    Summary TEXT NULL,
    PayloadJson TEXT NULL,
    TraceId TEXT NULL,
    OperatorUserId TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_task_plan_item_outputs (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    ConversationId TEXT NOT NULL,
    PlanId TEXT NOT NULL,
    ItemId TEXT NOT NULL,
    RunId TEXT NULL,
    OutputType TEXT NOT NULL DEFAULT 'Text',
    ResultSummary TEXT NOT NULL,
    Content TEXT NULL,
    EvidenceJson TEXT NULL,
    ErrorCode TEXT NULL,
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

        schema.EnsureColumn("ai_task_plans", "VersionNo", "INTEGER NOT NULL DEFAULT 1");
        schema.EnsureColumn("ai_task_plans", "Revision", "INTEGER NOT NULL DEFAULT 0");
        schema.EnsureColumn("ai_task_plans", "ExecutionStrategy", "TEXT NOT NULL DEFAULT 'Serial'");
        schema.EnsureColumn("ai_task_plans", "ApprovedBy", "TEXT NULL");
        schema.EnsureColumn("ai_task_plans", "ApprovedRevision", "INTEGER NULL");
        schema.EnsureColumn("ai_task_plan_items", "TaskType", "TEXT NOT NULL DEFAULT 'Design'");
        schema.EnsureColumn("ai_task_plan_items", "Depth", "INTEGER NOT NULL DEFAULT 0");
        schema.EnsureColumn("ai_task_plan_items", "DependsOnJson", "TEXT NULL");
        schema.EnsureColumn("ai_task_plan_items", "AcceptanceCriteriaJson", "TEXT NULL");
        schema.EnsureColumn("ai_task_plan_items", "ToolCode", "TEXT NULL");
        schema.EnsureColumn("ai_task_plan_items", "ExecutionHint", "TEXT NULL");
        schema.EnsureColumn("ai_task_plan_items", "ResultSummary", "TEXT NULL");
        schema.EnsureColumn("ai_task_plan_items", "EvidenceJson", "TEXT NULL");
        schema.EnsureColumn("ai_task_plan_items", "ErrorCode", "TEXT NULL");
        schema.EnsureColumn("ai_task_plan_items", "BlockedReason", "TEXT NULL");
        schema.EnsureColumn("ai_task_plan_items", "SkipReason", "TEXT NULL");
        schema.EnsureColumn("ai_task_plan_items", "RetryCount", "INTEGER NOT NULL DEFAULT 0");
        schema.EnsureColumn("ai_task_plan_items", "MaxRetryCount", "INTEGER NOT NULL DEFAULT 3");
    }


    private static void CreateGovernanceTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS ai_tool_execution_logs (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    ConversationId TEXT NULL,
    RunId TEXT NULL,
    ModelConfigId TEXT NULL,
    PlanId TEXT NULL,
    ItemId TEXT NULL,
    AgentProfileId TEXT NULL,
    ToolName TEXT NOT NULL,
    ToolCode TEXT NULL,
    ArgumentsJson TEXT NULL,
    ResultSummary TEXT NULL,
    RequiresConfirmation INTEGER NOT NULL DEFAULT 1,
    ConfirmedBy TEXT NULL,
    Status TEXT NOT NULL DEFAULT 'Pending',
    DurationMs INTEGER NOT NULL DEFAULT 0,
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

        schema.EnsureColumn("ai_tool_execution_logs", "PlanId", "TEXT NULL");
        schema.EnsureColumn("ai_tool_execution_logs", "ItemId", "TEXT NULL");
        schema.EnsureColumn("ai_tool_execution_logs", "ToolCode", "TEXT NULL");
        schema.EnsureColumn("ai_tool_execution_logs", "TraceId", "TEXT NULL");
        schema.EnsureColumn("ai_tool_execution_logs", "ModelConfigId", "TEXT NULL");

        schema.Execute("""
CREATE TABLE IF NOT EXISTS ai_usage_logs (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    UserId TEXT NOT NULL,
    ConversationId TEXT NULL,
    RunId TEXT NULL,
    ProviderCode TEXT NOT NULL,
    ModelCode TEXT NOT NULL,
    PromptTokens INTEGER NOT NULL DEFAULT 0,
    CompletionTokens INTEGER NOT NULL DEFAULT 0,
    ReasoningTokens INTEGER NOT NULL DEFAULT 0,
    TotalTokens INTEGER NOT NULL DEFAULT 0,
    CostAmount NUMERIC NOT NULL DEFAULT 0,
    DurationMs INTEGER NOT NULL DEFAULT 0,
    IsSuccess INTEGER NOT NULL DEFAULT 0,
    ErrorMessage TEXT NULL,
    RequestStartedAt TEXT NOT NULL,
    RequestCompletedAt TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_feedbacks (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    ConversationId TEXT NOT NULL,
    MessageId TEXT NOT NULL,
    RunId TEXT NULL,
    Rating TEXT NOT NULL,
    Comment TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_quota_policies (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    PolicyName TEXT NOT NULL,
    ScopeType TEXT NOT NULL DEFAULT 'Tenant',
    ScopeId TEXT NULL,
    MaxRequestsPerDay INTEGER NOT NULL DEFAULT 1000,
    MaxTokensPerDay INTEGER NOT NULL DEFAULT 1000000,
    MaxConcurrentRuns INTEGER NOT NULL DEFAULT 10,
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
CREATE TABLE IF NOT EXISTS ai_security_policies (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    PolicyKey TEXT NOT NULL,
    PolicyValue TEXT NOT NULL,
    Description TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_audit_events (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    EventType TEXT NOT NULL,
    ResourceType TEXT NOT NULL,
    ResourceId TEXT NULL,
    UserId TEXT NULL,
    DetailJson TEXT NULL,
    TraceId TEXT NULL,
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


    private static void CreateWorkflowToolTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS ai_workflow_draft_artifacts (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    ConversationId TEXT NOT NULL,
    RunId TEXT NULL,
    PlanId TEXT NULL,
    PlanItemId TEXT NULL,
    TraceId TEXT NOT NULL,
    WorkflowKey TEXT NOT NULL,
    WorkflowName TEXT NOT NULL,
    BusinessType TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Draft',
    DraftDslJson TEXT NOT NULL,
    BpmnXml TEXT NULL,
    BusinessCanvasJson TEXT NULL,
    BindingProposalJson TEXT NULL,
    FormPermissionProposalJson TEXT NULL,
    ActionMappingProposalJson TEXT NULL,
    NotificationPreviewJson TEXT NULL,
    ImportedWorkflowModelId TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_workflow_validation_reports (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    ConversationId TEXT NOT NULL,
    RunId TEXT NULL,
    PlanId TEXT NULL,
    PlanItemId TEXT NULL,
    DraftArtifactId TEXT NOT NULL,
    TraceId TEXT NOT NULL,
    IsValid INTEGER NOT NULL DEFAULT 0,
    ErrorCount INTEGER NOT NULL DEFAULT 0,
    WarningCount INTEGER NOT NULL DEFAULT 0,
    IssuesJson TEXT NOT NULL,
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
CREATE TABLE IF NOT EXISTS ai_workflow_simulation_reports (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    ConversationId TEXT NOT NULL,
    RunId TEXT NULL,
    PlanId TEXT NULL,
    PlanItemId TEXT NULL,
    DraftArtifactId TEXT NOT NULL,
    TraceId TEXT NOT NULL,
    Succeeded INTEGER NOT NULL DEFAULT 0,
    VariablesJson TEXT NOT NULL,
    StepsJson TEXT NOT NULL,
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
CREATE TABLE IF NOT EXISTS ai_workflow_diagnosis_reports (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    ConversationId TEXT NOT NULL,
    RunId TEXT NULL,
    PlanId TEXT NULL,
    PlanItemId TEXT NULL,
    TraceId TEXT NOT NULL,
    DiagnosisType TEXT NOT NULL,
    TargetId TEXT NOT NULL,
    Summary TEXT NOT NULL,
    EvidenceJson TEXT NOT NULL,
    SuggestionsJson TEXT NOT NULL,
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


    private static void CreateSkCapabilityTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS ai_sk_capability_status (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    CapabilityCode TEXT NOT NULL,
    Status TEXT NOT NULL,
    FrameworkType TEXT NOT NULL,
    ImplementationSymbol TEXT NOT NULL,
    Reason TEXT NOT NULL,
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


    private static void CreateKnowledgeTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS ai_knowledge_sources (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    SourceCode TEXT NOT NULL,
    SourceName TEXT NOT NULL,
    SourceType TEXT NOT NULL DEFAULT 'Document',
    Status TEXT NOT NULL DEFAULT 'Disabled',
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
CREATE TABLE IF NOT EXISTS ai_knowledge_documents (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    SourceId TEXT NOT NULL,
    DocumentName TEXT NOT NULL,
    ContentType TEXT NOT NULL,
    StoragePath TEXT NULL,
    IndexStatus TEXT NOT NULL DEFAULT 'Pending',
    ChunkCount INTEGER NOT NULL DEFAULT 0,
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
CREATE TABLE IF NOT EXISTS ai_knowledge_chunks (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    SourceId TEXT NOT NULL,
    DocumentId TEXT NOT NULL,
    ChunkIndex INTEGER NOT NULL DEFAULT 0,
    Content TEXT NOT NULL,
    MetadataJson TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_knowledge_graph_node_types (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    Code TEXT NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    Color TEXT NOT NULL DEFAULT '#2563eb',
    Icon TEXT NOT NULL DEFAULT 'circle',
    IsSystem INTEGER NOT NULL DEFAULT 0,
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
CREATE TABLE IF NOT EXISTS ai_knowledge_graph_relation_types (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    Code TEXT NOT NULL,
    Name TEXT NOT NULL,
    Directional INTEGER NOT NULL DEFAULT 1,
    Description TEXT NULL,
    Color TEXT NOT NULL DEFAULT '#64748b',
    IsSystem INTEGER NOT NULL DEFAULT 0,
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
CREATE TABLE IF NOT EXISTS ai_knowledge_graph_nodes (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    SourceId TEXT NULL,
    DocumentId TEXT NULL,
    NodeKey TEXT NOT NULL,
    NodeType TEXT NOT NULL,
    DisplayName TEXT NOT NULL,
    Summary TEXT NULL,
    MetadataJson TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_knowledge_graph_edges (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    SourceId TEXT NULL,
    FromNodeId TEXT NOT NULL,
    ToNodeId TEXT NOT NULL,
    RelationType TEXT NOT NULL,
    Weight NUMERIC NOT NULL DEFAULT 1,
    EvidenceText TEXT NULL,
    MetadataJson TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_knowledge_graph_evidence (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    SourceId TEXT NULL,
    DocumentId TEXT NULL,
    NodeId TEXT NULL,
    EdgeId TEXT NULL,
    EvidenceText TEXT NOT NULL,
    LocationJson TEXT NULL,
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
CREATE TABLE IF NOT EXISTS ai_knowledge_graph_build_jobs (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    SourceId TEXT NULL,
    RequestHash TEXT NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Pending',
    Progress INTEGER NOT NULL DEFAULT 0,
    CreatedCount INTEGER NOT NULL DEFAULT 0,
    UpdatedCount INTEGER NOT NULL DEFAULT 0,
    SkippedCount INTEGER NOT NULL DEFAULT 0,
    ErrorCode TEXT NULL,
    ErrorMessage TEXT NULL,
    StartedAt TEXT NULL,
    FinishedAt TEXT NULL,
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


    private static void CreateTaskProcessTables(SqliteSchemaExecutor schema)
    {
        schema.Execute("""
CREATE TABLE IF NOT EXISTS ai_task_process_states (
    Id TEXT NOT NULL PRIMARY KEY,
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    OwnerUserId TEXT NOT NULL,
    PlanId TEXT NOT NULL,
    RunId TEXT NOT NULL,
    ProcessStatus TEXT NOT NULL DEFAULT 'FrameworkUnavailable',
    ResumeToken TEXT NULL,
    StateJson TEXT NULL,
    ErrorCode TEXT NULL,
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
    }


}



using AsterERP.Api.Infrastructure.Database;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

internal static class AiCenterSchemaIndexMigrator
{
    public static void Migrate(SqliteSchemaExecutor schema)
    {
        CreateIndexes(schema);
    }

    private static void CreateIndexes(SqliteSchemaExecutor schema)
    {
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_providers_code ON ai_providers(TenantId, AppCode, ProviderCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_model_configs_code ON ai_model_configs(ProviderId, ModelCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_conversations_owner_time ON ai_conversations(IsDeleted, TenantId, AppCode, OwnerUserId, LastMessageAt);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_messages_conversation_seq ON ai_messages(ConversationId, Seq) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_messages_run ON ai_messages(RunId);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_chat_runs_active ON ai_chat_runs(ConversationId) WHERE IsDeleted = 0 AND Status IN ('Queued','Running','Cancelling');");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_chat_runs_client ON ai_chat_runs(ConversationId, ClientMessageId) WHERE IsDeleted = 0 AND ClientMessageId IS NOT NULL;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_chat_runs_status ON ai_chat_runs(IsDeleted, TenantId, AppCode, Status, CreatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_run_participants_agent ON ai_run_participants(RunId, AgentProfileId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_context_snapshots_conversation ON ai_context_snapshots(ConversationId, CreatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_prompt_templates_code ON ai_prompt_templates(TenantId, AppCode, TemplateCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_agent_profiles_code ON ai_agent_profiles(TenantId, AppCode, AgentCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_usage_logs_summary ON ai_usage_logs(IsDeleted, TenantId, AppCode, CreatedTime, ProviderCode, ModelCode);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_tool_logs_run ON ai_tool_execution_logs(RunId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_tool_logs_conversation ON ai_tool_execution_logs(IsDeleted, TenantId, AppCode, OwnerUserId, ConversationId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_task_plans_conversation ON ai_task_plans(IsDeleted, TenantId, AppCode, OwnerUserId, ConversationId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_task_plans_status ON ai_task_plans(IsDeleted, TenantId, AppCode, Status, UpdatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_task_plan_items_plan ON ai_task_plan_items(IsDeleted, PlanId, SortOrder);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_task_plan_items_status ON ai_task_plan_items(IsDeleted, PlanId, Status, OwnerType, SortOrder);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_task_plan_events_seq ON ai_task_plan_events(PlanId, Seq) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_task_plan_events_run ON ai_task_plan_events(IsDeleted, RunId, Seq);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_task_plan_outputs_item ON ai_task_plan_item_outputs(IsDeleted, PlanId, ItemId, CreatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_security_policies_key ON ai_security_policies(TenantId, AppCode, PolicyKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_audit_events_resource ON ai_audit_events(ResourceType, ResourceId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_workflow_drafts_conversation ON ai_workflow_draft_artifacts(IsDeleted, TenantId, AppCode, OwnerUserId, ConversationId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_workflow_drafts_plan ON ai_workflow_draft_artifacts(IsDeleted, TenantId, AppCode, OwnerUserId, PlanId, UpdatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_workflow_validation_draft ON ai_workflow_validation_reports(IsDeleted, DraftArtifactId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_workflow_simulation_draft ON ai_workflow_simulation_reports(IsDeleted, DraftArtifactId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_workflow_diagnosis_conversation ON ai_workflow_diagnosis_reports(IsDeleted, TenantId, AppCode, OwnerUserId, ConversationId, CreatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_sk_capabilities_code ON ai_sk_capability_status(TenantId, AppCode, CapabilityCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_knowledge_sources_code ON ai_knowledge_sources(TenantId, AppCode, SourceCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_knowledge_sources_owner ON ai_knowledge_sources(IsDeleted, TenantId, AppCode, OwnerUserId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_knowledge_documents_source ON ai_knowledge_documents(IsDeleted, TenantId, AppCode, OwnerUserId, SourceId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_knowledge_chunks_document ON ai_knowledge_chunks(IsDeleted, DocumentId, ChunkIndex);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_knowledge_graph_node_types_code ON ai_knowledge_graph_node_types(TenantId, AppCode, Code) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_knowledge_graph_relation_types_code ON ai_knowledge_graph_relation_types(TenantId, AppCode, Code) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_knowledge_graph_nodes_key ON ai_knowledge_graph_nodes(TenantId, AppCode, NodeKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_knowledge_graph_nodes_source ON ai_knowledge_graph_nodes(IsDeleted, TenantId, AppCode, OwnerUserId, SourceId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_knowledge_graph_nodes_type ON ai_knowledge_graph_nodes(IsDeleted, TenantId, AppCode, NodeType, DisplayName);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_knowledge_graph_edges_from ON ai_knowledge_graph_edges(IsDeleted, TenantId, AppCode, FromNodeId, RelationType);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_knowledge_graph_edges_to ON ai_knowledge_graph_edges(IsDeleted, TenantId, AppCode, ToNodeId, RelationType);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_knowledge_graph_edges_source ON ai_knowledge_graph_edges(IsDeleted, TenantId, AppCode, OwnerUserId, SourceId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_knowledge_graph_evidence_node ON ai_knowledge_graph_evidence(IsDeleted, TenantId, AppCode, NodeId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_knowledge_graph_evidence_edge ON ai_knowledge_graph_evidence(IsDeleted, TenantId, AppCode, EdgeId, CreatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_knowledge_graph_build_jobs_hash ON ai_knowledge_graph_build_jobs(TenantId, AppCode, SourceId, RequestHash) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_knowledge_graph_build_jobs_status ON ai_knowledge_graph_build_jobs(IsDeleted, TenantId, AppCode, OwnerUserId, Status, CreatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_task_process_states_run ON ai_task_process_states(RunId, PlanId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_system_settings_key ON ai_system_settings(TenantId, AppCode, SettingKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_prompt_versions_no ON ai_prompt_versions(PromptTemplateId, VersionNo) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_tool_definitions_code ON ai_tool_definitions(TenantId, AppCode, ToolCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_tool_bindings_agent_tool ON ai_tool_bindings(TenantId, AppCode, AgentProfileId, ToolCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_workflow_tool_bindings_workflow_tool ON ai_workflow_tool_bindings(TenantId, AppCode, WorkflowModelId, ToolCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_knowledge_index_tasks_owner ON ai_knowledge_index_tasks(IsDeleted, TenantId, AppCode, OwnerUserId, CreatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_secret_refs_code ON ai_secret_refs(TenantId, AppCode, SecretCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_workspaces_key ON ai_flowise_workspaces(TenantId, AppCode, WorkspaceKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_workspaces_owner ON ai_flowise_workspaces(IsDeleted, TenantId, AppCode, OwnerUserId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_chat_flows_type ON ai_flowise_chat_flows(IsDeleted, TenantId, AppCode, OwnerUserId, Type, UpdatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_chat_flows_workspace ON ai_flowise_chat_flows(IsDeleted, TenantId, AppCode, WorkspaceId, Type);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_chat_flows_name ON ai_flowise_chat_flows(IsDeleted, TenantId, AppCode, Type, Name);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_chat_flows_metadata ON ai_flowise_chat_flows(IsDeleted, TenantId, AppCode, Type, MetadataJson);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_sso_configs_key ON ai_flowise_sso_configs(TenantId, AppCode, ConfigKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_sso_configs_owner ON ai_flowise_sso_configs(IsDeleted, TenantId, AppCode, OwnerUserId, UpdatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_roles_key ON ai_flowise_roles(TenantId, AppCode, RoleKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_roles_owner ON ai_flowise_roles(IsDeleted, TenantId, AppCode, OwnerUserId, UpdatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_users_key ON ai_flowise_users(TenantId, AppCode, UserKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_users_owner ON ai_flowise_users(IsDeleted, TenantId, AppCode, OwnerUserId, UpdatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_login_activity_owner ON ai_flowise_login_activity(IsDeleted, TenantId, AppCode, OwnerUserId, CreatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_account_settings_owner ON ai_flowise_account_settings(TenantId, AppCode, OwnerUserId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_tools_key ON ai_flowise_tools(TenantId, AppCode, ToolKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_tools_owner ON ai_flowise_tools(IsDeleted, TenantId, AppCode, OwnerUserId, UpdatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_custom_mcp_servers_name ON ai_flowise_custom_mcp_servers(TenantId, AppCode, Name) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_custom_mcp_servers_owner ON ai_flowise_custom_mcp_servers(IsDeleted, TenantId, AppCode, OwnerUserId, Status, UpdatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_credentials_key ON ai_flowise_credentials(TenantId, AppCode, CredentialKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_credentials_owner ON ai_flowise_credentials(IsDeleted, TenantId, AppCode, OwnerUserId, UpdatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_variables_key ON ai_flowise_variables(TenantId, AppCode, VariableKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_variables_owner ON ai_flowise_variables(IsDeleted, TenantId, AppCode, OwnerUserId, UpdatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_api_keys_key ON ai_flowise_api_keys(TenantId, AppCode, ApiKeyCode) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_api_keys_owner ON ai_flowise_api_keys(IsDeleted, TenantId, AppCode, OwnerUserId, UpdatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_assistants_key ON ai_flowise_assistants(TenantId, AppCode, AssistantKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_assistants_owner ON ai_flowise_assistants(IsDeleted, TenantId, AppCode, OwnerUserId, AssistantType, UpdatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_marketplace_templates_key ON ai_flowise_marketplace_templates(TenantId, AppCode, TemplateKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_marketplace_templates_owner ON ai_flowise_marketplace_templates(IsDeleted, TenantId, AppCode, OwnerUserId, Category, UpdatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_document_stores_key ON ai_flowise_document_stores(TenantId, AppCode, StoreKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_document_stores_owner ON ai_flowise_document_stores(IsDeleted, TenantId, AppCode, OwnerUserId, Category, UpdatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_datasets_key ON ai_flowise_datasets(TenantId, AppCode, DatasetKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_datasets_owner ON ai_flowise_datasets(IsDeleted, TenantId, AppCode, OwnerUserId, Category, UpdatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_evaluators_key ON ai_flowise_evaluators(TenantId, AppCode, EvaluatorKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_evaluators_owner ON ai_flowise_evaluators(IsDeleted, TenantId, AppCode, OwnerUserId, EvaluatorType, UpdatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_evaluations_key ON ai_flowise_evaluations(TenantId, AppCode, EvaluationKey) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_evaluations_owner ON ai_flowise_evaluations(IsDeleted, TenantId, AppCode, OwnerUserId, Category, UpdatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_executions_resource ON ai_flowise_executions(IsDeleted, TenantId, AppCode, ResourceId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_executions_status ON ai_flowise_executions(IsDeleted, TenantId, AppCode, OwnerUserId, Status, CreatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_executions_idempotency ON ai_flowise_executions(TenantId, AppCode, ResourceId, IdempotencyKey) WHERE IsDeleted = 0 AND IdempotencyKey IS NOT NULL;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_schedule_records_target ON ai_flowise_schedule_records(TenantId, AppCode, TriggerType, TargetId, WorkspaceId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_schedule_records_enabled ON ai_flowise_schedule_records(IsDeleted, TenantId, AppCode, Enabled, NextRunAt);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_schedule_trigger_logs_record ON ai_flowise_schedule_trigger_logs(IsDeleted, TenantId, AppCode, ScheduleRecordId, ScheduledAt);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_schedule_trigger_logs_target ON ai_flowise_schedule_trigger_logs(IsDeleted, TenantId, AppCode, TargetId, ScheduledAt);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_audit_logs_resource ON ai_flowise_audit_logs(IsDeleted, TenantId, AppCode, ResourceType, ResourceId, CreatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_node_definitions_type ON ai_flowise_node_definitions(TenantId, AppCode, NodeType, Version) WHERE IsDeleted = 0;");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_shared_workspace_unique ON ai_flowise_shared_workspaces(TenantId, AppCode, ItemId, ItemType, SharedWorkspaceId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_shared_workspace_item ON ai_flowise_shared_workspaces(IsDeleted, TenantId, AppCode, ItemId, ItemType);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_chat_messages_chat ON ai_flowise_chat_messages(IsDeleted, TenantId, AppCode, ResourceId, ChatId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_feedback_message ON ai_flowise_feedback(IsDeleted, TenantId, AppCode, MessageId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_leads_resource ON ai_flowise_leads(IsDeleted, TenantId, AppCode, ResourceId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_doc_files_store ON ai_flowise_document_store_files(IsDeleted, TenantId, AppCode, StoreId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_doc_chunks_store ON ai_flowise_document_store_chunks(IsDeleted, TenantId, AppCode, StoreId, ChunkIndex);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_vector_configs_store ON ai_flowise_vector_store_configs(TenantId, AppCode, StoreId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_doc_upsert_history_store ON ai_flowise_document_store_upsert_history(IsDeleted, TenantId, AppCode, StoreId, CreatedTime);");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_ai_flowise_dataset_rows_dataset ON ai_flowise_dataset_rows(IsDeleted, TenantId, AppCode, DatasetId, CreatedTime);");
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_flowise_eval_results_version ON ai_flowise_evaluation_results(TenantId, AppCode, EvaluationId, VersionNo) WHERE IsDeleted = 0;");
    }

}



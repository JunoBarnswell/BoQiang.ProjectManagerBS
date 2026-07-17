using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.AsterScene;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.AsterScene;

public sealed class AsterSceneSchemaMigrator
{
    private static readonly Type[] EntityTypes =
    [
        typeof(AsterSceneProjectEntity), typeof(AsterSceneDocumentEntity),
        typeof(AsterSceneAssetEntity), typeof(AsterSceneAssetVersionEntity),
        typeof(AsterSceneUploadSessionEntity), typeof(AsterSceneUploadChunkEntity),
        typeof(AsterSceneJobEntity), typeof(AsterScenePublishVersionEntity),
        typeof(AsterScenePublicWorkEntity), typeof(AsterSceneCreatorProfileEntity),
        typeof(AsterSceneCommunityReactionEntity), typeof(AsterSceneRemixEntity),
        typeof(AsterSceneSubscriptionEntity), typeof(AsterSceneUsageLedgerEntity),
        typeof(AsterSceneModerationCaseEntity), typeof(AsterSceneAiCreditLedgerEntity),
        typeof(AsterSceneSupportTicketEntity), typeof(AsterSceneSupportCommentEntity)
        , typeof(AsterSceneModerationDecisionEntity), typeof(AsterSceneModerationEvidenceEntity)
        , typeof(AsterSceneAppealEntity), typeof(AsterSceneAppealDecisionEntity)
    ];

    public Task MigrateAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.CodeFirst.InitTables(EntityTypes);
        var schema = new SqliteSchemaExecutor(db);
        EnsureCompatibility(schema);
        RemoveRetiredSceneState(schema);
        CreateIndexes(schema);
        return Task.CompletedTask;
    }

    private static void EnsureCompatibility(SqliteSchemaExecutor schema)
    {
        schema.EnsureColumn("asterscene_asset", "OwnerUserId", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("asterscene_asset_version", "OwnerUserId", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("asterscene_upload_session", "OwnerUserId", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("asterscene_upload_chunk", "OwnerUserId", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("asterscene_job", "OwnerUserId", "TEXT NOT NULL DEFAULT ''");
        schema.EnsureColumn("asterscene_support_ticket", "ClientMutationId", "TEXT NULL");
        schema.Execute("UPDATE asterscene_asset SET OwnerUserId = COALESCE((SELECT OwnerUserId FROM asterscene_project WHERE Id = asterscene_asset.ProjectId), CreatedBy, '') WHERE OwnerUserId = '';");
        schema.Execute("UPDATE asterscene_asset_version SET OwnerUserId = COALESCE((SELECT OwnerUserId FROM asterscene_project WHERE Id = asterscene_asset_version.ProjectId), CreatedBy, '') WHERE OwnerUserId = '';");
        schema.Execute("UPDATE asterscene_upload_session SET OwnerUserId = COALESCE((SELECT OwnerUserId FROM asterscene_project WHERE Id = asterscene_upload_session.ProjectId), CreatedBy, '') WHERE OwnerUserId = '';");
        schema.Execute("UPDATE asterscene_upload_chunk SET OwnerUserId = COALESCE((SELECT OwnerUserId FROM asterscene_upload_session WHERE Id = asterscene_upload_chunk.UploadSessionId), CreatedBy, '') WHERE OwnerUserId = '';");
        schema.Execute("UPDATE asterscene_job SET OwnerUserId = COALESCE((SELECT OwnerUserId FROM asterscene_project WHERE Id = asterscene_job.ProjectId), CreatedBy, '') WHERE OwnerUserId = '';");
    }

    private static void RemoveRetiredSceneState(SqliteSchemaExecutor schema)
    {
        foreach (var table in new[] { "visual_tours", "visual_tour_drafts", "visual_tour_draft_histories", "visual_assets", "visual_asset_processing_tasks", "visual_published_snapshots", "visual_visit_events", "visual_settings", "visual_asset_processing_logs", "visual_templates", "visual_template_versions", "visual_plugins", "visual_collaboration_sessions", "visual_ai_scene_jobs", "visual_runtime_metric_rollups", "as_asset_processing_log", "as_template", "as_template_version", "as_plugin", "as_collaboration_session", "as_ai_scene_job", "as_runtime_metric_rollup", "as_model_geometry", "as_model_command", "as_material", "as_material_texture", "as_decal", "as_uv_layout", "as_model_export_job", "as_asset_version", "as_publish_manifest", "as_prefab", "as_interaction", "as_quality_check_result" })
        {
            schema.Execute($"DROP TABLE IF EXISTS {SqliteSchemaExecutor.QuoteIdentifier(table)};");
        }
        schema.Execute("DELETE FROM system_role_permissions WHERE PermissionCodeId IN (SELECT Id FROM system_permission_codes WHERE PermissionCode LIKE 'virtual:%');");
        schema.Execute("DELETE FROM system_permission_codes WHERE PermissionCode LIKE 'virtual:%';");
        schema.Execute("DELETE FROM system_menus WHERE MenuCode = 'virtual' OR MenuCode LIKE 'virtual:%' OR RoutePath LIKE '/virtual-exhibition%';");
    }

    private static void CreateIndexes(SqliteSchemaExecutor schema)
    {
        schema.Execute("DROP INDEX IF EXISTS ux_as_publish_code;");
        schema.Execute("DROP INDEX IF EXISTS ux_as_public_slug;");
        schema.Execute("DROP INDEX IF EXISTS ux_as_creator_handle;");
        var indexes = new[]
        {
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_project_workspace_code ON asterscene_project(TenantId, AppCode, ProjectCode) WHERE IsDeleted = 0;",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_project_mutation ON asterscene_project(TenantId, AppCode, OwnerUserId, CreateClientMutationId) WHERE IsDeleted = 0 AND CreateClientMutationId IS NOT NULL;",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_document_revision ON asterscene_document(TenantId, AppCode, ProjectId, Revision) WHERE IsDeleted = 0;",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_document_mutation ON asterscene_document(TenantId, AppCode, ProjectId, ClientMutationId) WHERE IsDeleted = 0 AND ClientMutationId IS NOT NULL;",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_asset_code ON asterscene_asset(TenantId, AppCode, OwnerUserId, ProjectId, AssetCode) WHERE IsDeleted = 0;",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_asset_mutation ON asterscene_asset(TenantId, AppCode, OwnerUserId, ProjectId, ClientMutationId) WHERE IsDeleted = 0 AND ClientMutationId IS NOT NULL;",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_publish_code ON asterscene_publish_version(PublishCode) WHERE IsDeleted = 0;",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_publish_project_version ON asterscene_publish_version(TenantId, AppCode, ProjectId, Version) WHERE IsDeleted = 0;",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_public_slug ON asterscene_public_work(Slug) WHERE IsDeleted = 0;",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_creator_handle ON asterscene_creator_profile(Handle) WHERE IsDeleted = 0;",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_creator_user ON asterscene_creator_profile(TenantId, AppCode, UserId) WHERE IsDeleted = 0;",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_reaction ON asterscene_community_reaction(TenantId, AppCode, WorkId, UserId, ReactionType) WHERE IsDeleted = 0;",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_subscription_owner ON asterscene_subscription(TenantId, AppCode, OwnerUserId) WHERE IsDeleted = 0;",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_usage_idempotency ON asterscene_usage_ledger(TenantId, AppCode, IdempotencyKey) WHERE IsDeleted = 0;",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_ai_credit_idempotency ON asterscene_ai_credit_ledger(TenantId, AppCode, IdempotencyKey) WHERE IsDeleted = 0;",
            "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_moderation_report_mutation ON asterscene_moderation_case(TenantId, AppCode, ReporterUserId, ClientMutationId) WHERE IsDeleted = 0 AND ClientMutationId IS NOT NULL;",
            "CREATE INDEX IF NOT EXISTS idx_as_support_comment_ticket ON asterscene_support_comment(TenantId, AppCode, OwnerUserId, TicketId, CreatedTime) WHERE IsDeleted = 0;"
            , "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_moderation_decision_mutation ON asterscene_moderation_decision(TenantId, AppCode, CaseId, ClientMutationId) WHERE IsDeleted = 0;"
            , "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_moderation_evidence_mutation ON asterscene_moderation_evidence(TenantId, AppCode, CaseId, ClientMutationId) WHERE IsDeleted = 0;"
            , "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_appeal_mutation ON asterscene_appeal(TenantId, AppCode, CaseId, AppellantUserId, ClientMutationId) WHERE IsDeleted = 0;"
            , "CREATE UNIQUE INDEX IF NOT EXISTS ux_as_appeal_decision_mutation ON asterscene_appeal_decision(TenantId, AppCode, AppealId, ClientMutationId) WHERE IsDeleted = 0;"
            , "CREATE INDEX IF NOT EXISTS idx_as_moderation_history_case ON asterscene_moderation_decision(TenantId, AppCode, CaseId, CreatedTime) WHERE IsDeleted = 0;"
            , "CREATE INDEX IF NOT EXISTS idx_as_moderation_evidence_case ON asterscene_moderation_evidence(TenantId, AppCode, CaseId, CreatedTime) WHERE IsDeleted = 0;"
            , "CREATE INDEX IF NOT EXISTS idx_as_appeal_case ON asterscene_appeal(TenantId, AppCode, CaseId, CreatedTime) WHERE IsDeleted = 0;"
        };
        foreach (var index in indexes) schema.Execute(index);
    }
}

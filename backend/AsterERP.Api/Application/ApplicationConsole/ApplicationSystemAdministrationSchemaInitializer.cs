using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.Runtime;
using AsterERP.Api.Modules.System.Announcements;
using AsterERP.Api.Modules.System.Auth;
using AsterERP.Api.Modules.System.CodeRules;
using AsterERP.Api.Modules.System.Dicts;
using AsterERP.Api.Modules.System.Files;
using AsterERP.Api.Modules.System.Logs;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Modules.System.Messaging;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Parameters;
using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Api.Modules.System.Printing;
using AsterERP.Api.Modules.System.QueryViews;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.ScheduledJobs;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Api.Modules.Workflows;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationConsole;

public sealed class ApplicationSystemAdministrationSchemaInitializer
{
    public void Initialize(ISqlSugarClient appDb)
    {
        appDb.CodeFirst.InitTables(
            typeof(SystemPermissionCodeEntity),
            typeof(SystemRoleEntity),
            typeof(SystemRolePermissionEntity),
            typeof(SystemUserRoleEntity),
            typeof(SystemUserAppRoleEntity),
            typeof(SystemUserEntity),
            typeof(SystemUserEmploymentEntity),
            typeof(SystemAuthSessionEntity),
            typeof(SystemDepartmentEntity),
            typeof(SystemPositionEntity),
            typeof(SystemMenuEntity),
            typeof(SystemDictTypeEntity),
            typeof(SystemDictItemEntity),
            typeof(SystemParameterEntity),
            typeof(SystemAnnouncementEntity),
            typeof(SystemOperationLogEntity),
            typeof(SystemLoginLogEntity),
            typeof(SystemMessageSendLogEntity),
            typeof(SystemScheduledJobEntity),
            typeof(SystemScheduledJobLogEntity),
            typeof(SystemCodeRuleEntity),
            typeof(SystemCodeRuleSegmentEntity),
            typeof(SystemFileRecordEntity),
            typeof(SystemPrintTemplateEntity),
            typeof(SystemPrintCustomElementEntity),
            typeof(SystemQueryViewTableResourceEntity),
            typeof(SystemQueryViewColumnResourceEntity),
            typeof(SystemQueryViewDefinitionEntity),
            typeof(SystemQueryViewRuntimeEntity),
            typeof(SystemQueryViewPublishLogEntity),
            typeof(SystemQueryViewExportTaskEntity),
            typeof(SystemQueryViewUserPreferenceEntity),
            typeof(SystemDataModelEntity),
            typeof(SystemTenantGridViewEntity),
            typeof(SystemUserGridViewEntity),
            typeof(ApplicationDataSourceEntity),
            typeof(ApplicationConnectionCheckTaskEntity),
            typeof(ApplicationConnectionCheckRunEntity),
            typeof(ApplicationDataModelDesignEntity),
            typeof(ApplicationMicroflowEntity),
            typeof(ApplicationDataEntityDefinitionEntity),
            typeof(ApplicationDataFieldDefinitionEntity),
            typeof(ApplicationDataCenterDictionaryEntity),
            typeof(ApplicationApiServiceEntity),
            typeof(ApplicationQueryDatasetEntity),
            typeof(ApplicationIntegrationTaskEntity),
            typeof(ApplicationIntegrationTaskRunEntity),
            typeof(ApplicationDataObjectReferenceEntity),
            typeof(ApplicationDataImportBatchEntity),
            typeof(ApplicationSqlScriptAuditEntity),
            typeof(ApplicationDevelopmentVersionEntity),
            typeof(ApplicationDevelopmentModuleEntity),
            typeof(ApplicationDevelopmentPageEntity),
            typeof(ApplicationSharedResourceEntity),
            typeof(ApplicationBusinessObjectDesignEntity),
            typeof(ApplicationDesignerDocumentEntity),
            typeof(ApplicationDesignerRevisionEntity),
            typeof(ApplicationDesignerMigrationEntity),
            typeof(ApplicationDesignerRuntimeArtifactEntity),
            typeof(ApplicationDesignerEditorSessionEntity),
            typeof(ApplicationDesignerPublishRecordEntity),
            typeof(ApplicationDesignerMigrationRunEntity),
            typeof(ApplicationDesignerMigrationWatermarkEntity),
            typeof(ApplicationMonitoringEventEntity),
            typeof(WorkflowBindingEntity),
            typeof(WorkflowBusinessInstanceEntity),
            typeof(WorkflowCallbackLogEntity),
            typeof(WorkflowCategoryEntity),
            typeof(WorkflowDelegationRuleEntity),
            typeof(WorkflowMessageTemplateEntity),
            typeof(WorkflowModelExtensionEntity),
            typeof(WorkflowNodeNotificationRuleEntity),
            typeof(WorkflowNotificationChannelEntity),
            typeof(WorkflowNotificationLogEntity),
            typeof(WorkflowNotificationTaskEntity),
            typeof(WorkflowRequestDraftEntity),
            typeof(WorkflowWorkCalendarEntity),
            typeof(SystemApplicationPublishTaskEntity),
            typeof(SystemApplicationPublishLogEntity),
            typeof(SystemApplicationPublishArtifactEntity),
            typeof(SystemApplicationPublishProfileEntity));

        EnsureApplicationSystemSchemaColumns(appDb);
    }

    private static void EnsureApplicationSystemSchemaColumns(ISqlSugarClient appDb)
    {
        var schema = new SqliteSchemaExecutor(appDb);
        schema.EnsureColumn("app_dev_pages", "PageParametersJson", "TEXT NOT NULL DEFAULT '[]'");
        schema.EnsureColumn("app_dev_pages", "PageType", "TEXT NOT NULL DEFAULT 'standard'");
        schema.EnsureColumn("app_dev_pages", "ParentPageId", "TEXT NULL");
        schema.EnsureColumn("system_departments", "LeaderUserIdsJson", "TEXT NULL");
        schema.EnsureColumn("system_users", "PasswordResetRequired", "INTEGER NOT NULL DEFAULT 0");
        schema.EnsureColumn("system_users", "PasswordFormatVersion", "TEXT NOT NULL DEFAULT 'legacy-unknown'");
        schema.Execute("""
CREATE TABLE IF NOT EXISTS system_user_employments (
    Id TEXT NOT NULL PRIMARY KEY,
    UserId TEXT NOT NULL,
    TenantId TEXT NOT NULL DEFAULT 'tenant-system',
    AppCode TEXT NOT NULL DEFAULT 'SYSTEM',
    DeptId TEXT NOT NULL,
    PositionId TEXT NOT NULL,
    EmploymentName TEXT NOT NULL,
    IsPrimary INTEGER NOT NULL DEFAULT 0,
    Status TEXT NOT NULL DEFAULT 'Enabled',
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
        schema.Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_system_user_employments_unique ON system_user_employments(UserId, TenantId, AppCode, DeptId, PositionId) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_user_employments_user ON system_user_employments(UserId, TenantId, AppCode, Status) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_user_employments_dept ON system_user_employments(DeptId, Status) WHERE IsDeleted = 0;");
        schema.Execute("CREATE INDEX IF NOT EXISTS idx_system_user_employments_position ON system_user_employments(PositionId, Status) WHERE IsDeleted = 0;");
        schema.Execute("""
INSERT INTO system_user_employments (
    Id, UserId, TenantId, AppCode, DeptId, PositionId, EmploymentName, IsPrimary, Status, SortOrder,
    CreatedBy, CreatedTime, UpdatedBy, UpdatedTime, DeletedBy, DeletedTime, IsDeleted, Remark
)
SELECT
    lower(hex(randomblob(16))),
    u.Id,
    'tenant-system',
    'SYSTEM',
    u.DeptId,
    u.PositionId,
    coalesce(d.DeptName, u.DeptId) || '/' || coalesce(p.PositionName, u.PositionId),
    1,
    u.Status,
    1,
    u.CreatedBy,
    u.CreatedTime,
    u.UpdatedBy,
    u.UpdatedTime,
    NULL,
    NULL,
    0,
    'Migrated from system_users.DeptId/PositionId'
FROM system_users u
LEFT JOIN system_departments d ON d.Id = u.DeptId AND d.IsDeleted = 0
LEFT JOIN system_positions p ON p.Id = u.PositionId AND p.IsDeleted = 0
WHERE u.IsDeleted = 0
  AND u.DeptId IS NOT NULL AND u.DeptId <> ''
  AND u.PositionId IS NOT NULL AND u.PositionId <> ''
  AND NOT EXISTS (
      SELECT 1
      FROM system_user_employments e
      WHERE e.UserId = u.Id
        AND e.TenantId = 'tenant-system'
        AND e.AppCode = 'SYSTEM'
        AND e.DeptId = u.DeptId
        AND e.PositionId = u.PositionId
        AND e.IsDeleted = 0
  );
""");
    }
}

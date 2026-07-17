using AsterERP.Api.Modules.System.Permissions;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Shared;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

internal static class AiCenterPermissionSeeder
{
    private const string ModuleName = "AI Center";

    public static void Seed(ISqlSugarClient db)
    {
        foreach (var (code, name) in PermissionDefinitions)
        {
            UpsertPermission(db, code, name);
        }

        EnsureApplicationAdminRolePermissions(db);
    }

    private static void UpsertPermission(ISqlSugarClient db, string code, string name)
    {
        var existingRows = db.Queryable<SystemPermissionCodeEntity>()
            .Where(item => item.PermissionCode == code)
            .ToList();
        var existing = existingRows
            .OrderBy(item => item.IsDeleted)
            .ThenBy(item => item.CreatedTime)
            .FirstOrDefault();
        if (existing is null)
        {
            db.Insertable(new SystemPermissionCodeEntity
            {
                ModuleName = ModuleName,
                PermissionCode = code,
                PermissionName = name,
                IsEnabled = true
            }).ExecuteCommand();
            return;
        }

        var duplicateRows = existingRows
            .Where(item => item.Id != existing.Id)
            .ToArray();
        SoftDeleteDuplicatePermissions(db, duplicateRows);

        existing.ModuleName = ModuleName;
        existing.PermissionName = name;
        existing.IsEnabled = true;
        existing.IsDeleted = false;
        existing.DeletedBy = null;
        existing.DeletedTime = null;
        existing.UpdatedTime = DateTime.UtcNow;
        db.Updateable(existing).ExecuteCommand();
    }

    private static void SoftDeleteDuplicatePermissions(
        ISqlSugarClient db,
        IReadOnlyCollection<SystemPermissionCodeEntity> duplicateRows)
    {
        if (duplicateRows.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var duplicateIds = duplicateRows.Select(item => item.Id).ToArray();
        var duplicateRolePermissions = db.Queryable<SystemRolePermissionEntity>()
            .Where(item => duplicateIds.Contains(item.PermissionCodeId) && !item.IsDeleted)
            .ToList();

        foreach (var entity in duplicateRows)
        {
            entity.IsEnabled = false;
            entity.IsDeleted = true;
            entity.DeletedTime = now;
            entity.UpdatedTime = now;
        }

        foreach (var entity in duplicateRolePermissions)
        {
            entity.IsDeleted = true;
            entity.DeletedTime = now;
            entity.UpdatedTime = now;
        }

        db.Updateable(duplicateRows.ToList()).ExecuteCommand();
        if (duplicateRolePermissions.Count > 0)
        {
            db.Updateable(duplicateRolePermissions).ExecuteCommand();
        }
    }

    private static void EnsureApplicationAdminRolePermissions(ISqlSugarClient db)
    {
        foreach (var roleCode in ApplicationAdminRoleCodes)
        {
            EnsureRolePermissions(db, roleCode, ApplicationAdminPermissionCodes);
        }
    }

    private static void EnsureRolePermissions(
        ISqlSugarClient db,
        string roleCode,
        IReadOnlyCollection<string> permissionCodes)
    {
        var role = db.Queryable<SystemRoleEntity>()
            .First(item => item.RoleCode == roleCode && !item.IsDeleted);
        if (role is null)
        {
            return;
        }

        var permissions = db.Queryable<SystemPermissionCodeEntity>()
            .Where(item => permissionCodes.Contains(item.PermissionCode) && !item.IsDeleted && item.IsEnabled)
            .ToList();
        if (permissions.Count == 0)
        {
            return;
        }

        var permissionIds = permissions.Select(item => item.Id).ToArray();
        var existingPermissionIds = db.Queryable<SystemRolePermissionEntity>()
            .Where(item => item.RoleId == role.Id && permissionIds.Contains(item.PermissionCodeId) && !item.IsDeleted)
            .Select(item => item.PermissionCodeId)
            .ToList();
        var records = permissions
            .Where(item => !existingPermissionIds.Contains(item.Id))
            .Select(item => new SystemRolePermissionEntity
            {
                RoleId = role.Id,
                PermissionCodeId = item.Id
            })
            .ToArray();

        if (records.Length > 0)
        {
            db.Insertable(records).ExecuteCommand();
        }
    }

    private static readonly IReadOnlyList<(string Code, string Name)> PermissionDefinitions =
    [
        (PermissionCodes.AiWorkbenchView, "AI Workbench View"),
        (PermissionCodes.AiCapabilityView, "AI Capability Center View"),
        (PermissionCodes.AiObservabilityView, "AI Observability View"),
        (PermissionCodes.AiSecurityView, "AI Security View"),
        (PermissionCodes.AiSecurityEdit, "AI Security Edit"),
        (PermissionCodes.AiSettingsView, "AI Settings View"),
        (PermissionCodes.AiSettingsEdit, "AI Settings Edit"),
        (PermissionCodes.AiChatView, "AI Chat View"),
        (PermissionCodes.AiChatCreate, "AI Chat Create"),
        (PermissionCodes.AiChatDelete, "AI Chat Delete"),
        (PermissionCodes.AiChatArchive, "AI Chat Archive"),
        (PermissionCodes.AiChatCompress, "AI Chat Compress"),
        (PermissionCodes.AiChatViewAll, "AI Chat View All"),
        (PermissionCodes.AiConversationView, "AI Conversation View"),
        (PermissionCodes.AiConversationCreate, "AI Conversation Create"),
        (PermissionCodes.AiConversationEdit, "AI Conversation Edit"),
        (PermissionCodes.AiConversationArchive, "AI Conversation Archive"),
        (PermissionCodes.AiConversationDelete, "AI Conversation Delete"),
        (PermissionCodes.AiConversationViewAll, "AI Conversation View All"),
        (PermissionCodes.AiTaskPlanCreate, "AI Task Plan Create"),
        (PermissionCodes.AiTaskPlanEdit, "AI Task Plan Edit"),
        (PermissionCodes.AiTaskPlanDelete, "AI Task Plan Delete"),
        (PermissionCodes.AiTaskPlanApprove, "AI Task Plan Approve"),
        (PermissionCodes.AiTaskPlanExecute, "AI Task Plan Execute"),
        (PermissionCodes.AiTaskPlanRetry, "AI Task Plan Retry"),
        (PermissionCodes.AiTaskPlanSkip, "AI Task Plan Skip"),
        (PermissionCodes.AiTaskPlanLogView, "AI Task Plan Log View"),
        (PermissionCodes.AiTaskPlanManageAll, "AI Task Plan Manage All"),
        (PermissionCodes.AiModelView, "AI Model View"),
        (PermissionCodes.AiModelAdd, "AI Model Add"),
        (PermissionCodes.AiModelEdit, "AI Model Edit"),
        (PermissionCodes.AiModelDelete, "AI Model Delete"),
        (PermissionCodes.AiModelDisable, "AI Model Disable"),
        (PermissionCodes.AiModelTest, "AI Model Test"),
        (PermissionCodes.AiModelCopy, "AI Model Copy"),
        (PermissionCodes.AiProviderView, "AI Provider View"),
        (PermissionCodes.AiProviderAdd, "AI Provider Add"),
        (PermissionCodes.AiProviderEdit, "AI Provider Edit"),
        (PermissionCodes.AiProviderDelete, "AI Provider Delete"),
        (PermissionCodes.AiProviderDisable, "AI Provider Disable"),
        (PermissionCodes.AiProviderTest, "AI Provider Test"),
        (PermissionCodes.AiPromptView, "AI Prompt View"),
        (PermissionCodes.AiPromptAdd, "AI Prompt Add"),
        (PermissionCodes.AiPromptEdit, "AI Prompt Edit"),
        (PermissionCodes.AiPromptDelete, "AI Prompt Delete"),
        (PermissionCodes.AiPromptCopy, "AI Prompt Copy"),
        (PermissionCodes.AiPromptPublish, "AI Prompt Publish"),
        (PermissionCodes.AiPromptTest, "AI Prompt Test"),
        (PermissionCodes.AiAgentView, "AI Agent View"),
        (PermissionCodes.AiAgentAdd, "AI Agent Add"),
        (PermissionCodes.AiAgentEdit, "AI Agent Edit"),
        (PermissionCodes.AiAgentDelete, "AI Agent Delete"),
        (PermissionCodes.AiAgentCopy, "AI Agent Copy"),
        (PermissionCodes.AiAgentDisable, "AI Agent Disable"),
        (PermissionCodes.AiAgentTest, "AI Agent Test"),
        (PermissionCodes.AiLogView, "AI Log View"),
        (PermissionCodes.AiUsageView, "AI Usage View"),
        (PermissionCodes.AiSecurityManage, "AI Security Manage"),
        (PermissionCodes.AiKnowledgeView, "AI Knowledge View"),
        (PermissionCodes.AiKnowledgeManage, "AI Knowledge Manage"),
        (PermissionCodes.AiKnowledgeUpload, "AI Knowledge Upload"),
        (PermissionCodes.AiKnowledgeIndex, "AI Knowledge Index"),
        (PermissionCodes.AiKnowledgeTest, "AI Knowledge Test"),
        (PermissionCodes.AiKnowledgeGraphView, "AI Knowledge Graph View"),
        (PermissionCodes.AiKnowledgeGraphSearch, "AI Knowledge Graph Search"),
        (PermissionCodes.AiKnowledgeGraphEdit, "AI Knowledge Graph Edit"),
        (PermissionCodes.AiKnowledgeGraphImport, "AI Knowledge Graph Import"),
        (PermissionCodes.AiKnowledgeGraphExport, "AI Knowledge Graph Export"),
        (PermissionCodes.AiKnowledgeGraphReindex, "AI Knowledge Graph Reindex"),
        (PermissionCodes.AiToolView, "AI Tool View"),
        (PermissionCodes.AiToolAdd, "AI Tool Add"),
        (PermissionCodes.AiToolEdit, "AI Tool Edit"),
        (PermissionCodes.AiToolBindWorkflow, "AI Tool Bind Workflow"),
        (PermissionCodes.AiToolExecute, "AI Tool Execute"),
        (PermissionCodes.AiToolConfirmHighRisk, "AI Tool Confirm High Risk"),
        (PermissionCodes.AiToolWorkflowView, "AI Workflow Tool View"),
        (PermissionCodes.AiToolWorkflowRead, "AI Workflow Tool Read"),
        (PermissionCodes.AiToolWorkflowDraft, "AI Workflow Tool Draft"),
        (PermissionCodes.AiToolWorkflowValidate, "AI Workflow Tool Validate"),
        (PermissionCodes.AiToolWorkflowSimulate, "AI Workflow Tool Simulate"),
        (PermissionCodes.AiToolWorkflowDiagnose, "AI Workflow Tool Diagnose"),
        (PermissionCodes.AiToolWorkflowImportDraft, "AI Workflow Tool Import Draft"),
        (PermissionCodes.AiToolWorkflowPublishRequest, "AI Workflow Tool Publish Request"),
        (PermissionCodes.AiToolSystemAdminView, "AI System Admin Tool View"),
        (PermissionCodes.AiToolSystemAdminRead, "AI System Admin Tool Read"),
        (PermissionCodes.AiToolSystemAdminWrite, "AI System Admin Tool Write"),
        (PermissionCodes.AiToolSystemAdminGrant, "AI System Admin Tool Grant"),
        (PermissionCodes.AiToolSystemAdminOperate, "AI System Admin Tool Operate"),
        (PermissionCodes.AiToolDataCenterView, "AI Data Center Tool View"),
        (PermissionCodes.AiToolDataCenterRead, "AI Data Center Tool Read"),
        (PermissionCodes.AiToolDataCenterWrite, "AI Data Center Tool Write"),
        (PermissionCodes.AiToolDataCenterOperate, "AI Data Center Tool Operate"),
        (PermissionCodes.FlowiseView, "Flowise Studio View"),
        (PermissionCodes.FlowiseEdit, "Flowise Studio Edit"),
        (PermissionCodes.FlowiseRun, "Flowise Studio Run"),
        (PermissionCodes.FlowiseImport, "Flowise Studio Import"),
        (PermissionCodes.FlowiseExport, "Flowise Studio Export"),
        (PermissionCodes.FlowiseManage, "Flowise Studio Manage"),
        (PermissionCodes.FlowiseRevealSecret, "Flowise Secret Reveal"),
        (PermissionCodes.FlowiseShare, "Flowise Studio Share"),
        (PermissionCodes.FlowiseRetry, "Flowise Studio Retry"),
        (PermissionCodes.FlowiseSchedule, "Flowise Studio Schedule"),
        (PermissionCodes.FlowiseWebhook, "Flowise Studio Webhook"),
        (PermissionCodes.FlowiseChatflowsView, "Flowise Chatflows View"),
        (PermissionCodes.FlowiseChatflowsEdit, "Flowise Chatflows Edit"),
        (PermissionCodes.FlowiseChatflowsDuplicate, "Flowise Chatflows Duplicate"),
        (PermissionCodes.FlowiseChatflowsExport, "Flowise Chatflows Export"),
        (PermissionCodes.FlowiseChatflowsConfig, "Flowise Chatflows Config"),
        (PermissionCodes.FlowiseChatflowsDomains, "Flowise Chatflows Domains"),
        (PermissionCodes.FlowiseChatflowsDelete, "Flowise Chatflows Delete"),
        (PermissionCodes.FlowiseChatflowsRun, "Flowise Chatflows Run"),
        (PermissionCodes.FlowiseChatflowsShare, "Flowise Chatflows Share"),
        (PermissionCodes.FlowiseChatflowsTest, "Flowise Chatflows Test"),
        (PermissionCodes.FlowiseAgentflowsView, "Flowise Agentflows View"),
        (PermissionCodes.FlowiseAgentflowsEdit, "Flowise Agentflows Edit"),
        (PermissionCodes.FlowiseAgentflowsDuplicate, "Flowise Agentflows Duplicate"),
        (PermissionCodes.FlowiseAgentflowsExport, "Flowise Agentflows Export"),
        (PermissionCodes.FlowiseAgentflowsConfig, "Flowise Agentflows Config"),
        (PermissionCodes.FlowiseAgentflowsDomains, "Flowise Agentflows Domains"),
        (PermissionCodes.FlowiseAgentflowsDelete, "Flowise Agentflows Delete"),
        (PermissionCodes.FlowiseAgentflowsRun, "Flowise Agentflows Run"),
        (PermissionCodes.FlowiseTemplatesFlowExport, "Flowise Template Flow Export"),
        (PermissionCodes.FlowiseExecutionsView, "Flowise Executions View"),
        (PermissionCodes.FlowiseExecutionsManage, "Flowise Executions Manage"),
        (PermissionCodes.FlowiseAssistantsView, "Flowise Assistants View"),
        (PermissionCodes.FlowiseAssistantsEdit, "Flowise Assistants Edit"),
        (PermissionCodes.FlowiseMarketplacesView, "Flowise Marketplaces View"),
        (PermissionCodes.FlowiseMarketplacesEdit, "Flowise Marketplaces Edit"),
        (PermissionCodes.FlowiseToolsView, "Flowise Tools View"),
        (PermissionCodes.FlowiseToolsCreate, "Flowise Tools Create"),
        (PermissionCodes.FlowiseToolsEdit, "Flowise Tools Edit"),
        (PermissionCodes.FlowiseToolsUpdate, "Flowise Tools Update"),
        (PermissionCodes.FlowiseToolsDelete, "Flowise Tools Delete"),
        (PermissionCodes.FlowiseCredentialsView, "Flowise Credentials View"),
        (PermissionCodes.FlowiseCredentialsEdit, "Flowise Credentials Edit"),
        (PermissionCodes.FlowiseVariablesView, "Flowise Variables View"),
        (PermissionCodes.FlowiseVariablesEdit, "Flowise Variables Edit"),
        (PermissionCodes.FlowiseApiKeysView, "Flowise API Keys View"),
        (PermissionCodes.FlowiseApiKeysEdit, "Flowise API Keys Edit"),
        (PermissionCodes.FlowiseDocumentStoresView, "Flowise Document Stores View"),
        (PermissionCodes.FlowiseDocumentStoresEdit, "Flowise Document Stores Edit"),
        (PermissionCodes.FlowiseDocumentStoresUpsert, "Flowise Document Stores Upsert"),
        (PermissionCodes.FlowiseDatasetsView, "Flowise Datasets View"),
        (PermissionCodes.FlowiseDatasetsEdit, "Flowise Datasets Edit"),
        (PermissionCodes.FlowiseEvaluatorsView, "Flowise Evaluators View"),
        (PermissionCodes.FlowiseEvaluatorsEdit, "Flowise Evaluators Edit"),
        (PermissionCodes.FlowiseEvaluationsView, "Flowise Evaluations View"),
        (PermissionCodes.FlowiseEvaluationsEdit, "Flowise Evaluations Edit"),
        (PermissionCodes.FlowiseSsoManage, "Flowise SSO Config Manage"),
        (PermissionCodes.FlowiseRolesManage, "Flowise Roles Manage"),
        (PermissionCodes.FlowiseUsersManage, "Flowise Users Manage"),
        (PermissionCodes.FlowiseWorkspacesView, "Flowise Workspaces View"),
        (PermissionCodes.FlowiseWorkspacesManage, "Flowise Workspaces Manage"),
        (PermissionCodes.FlowiseLoginActivityView, "Flowise Login Activity View"),
        (PermissionCodes.FlowiseLoginActivityManage, "Flowise Login Activity Manage"),
        (PermissionCodes.FlowiseLogsView, "Flowise Logs View"),
        (PermissionCodes.FlowiseLogsManage, "Flowise Logs Manage"),
        (PermissionCodes.FlowiseLogsRead, "Flowise Logs Read"),
        (PermissionCodes.FlowiseAccountView, "Flowise Account View"),
        (PermissionCodes.FlowiseAccountEdit, "Flowise Account Edit")
    ];

    private static readonly IReadOnlyList<string> ApplicationAdminRoleCodes =
    [
        "wms_admin",
        "mes_admin"
    ];

    private static readonly IReadOnlyList<string> ApplicationAdminPermissionCodes =
    [
        PermissionCodes.AiWorkbenchView,
        PermissionCodes.AiSettingsView,
        PermissionCodes.AiChatView,
        PermissionCodes.AiChatCreate,
        PermissionCodes.AiChatDelete,
        PermissionCodes.AiChatArchive,
        PermissionCodes.AiConversationView,
        PermissionCodes.AiConversationCreate,
        PermissionCodes.AiConversationEdit,
        PermissionCodes.AiConversationArchive,
        PermissionCodes.AiConversationDelete,
        PermissionCodes.AiModelView,
        PermissionCodes.AiModelAdd,
        PermissionCodes.AiModelEdit,
        PermissionCodes.AiModelDelete,
        PermissionCodes.AiModelDisable,
        PermissionCodes.AiModelTest,
        PermissionCodes.AiModelCopy,
        PermissionCodes.AiProviderView,
        PermissionCodes.AiProviderAdd,
        PermissionCodes.AiProviderEdit,
        PermissionCodes.AiProviderDelete,
        PermissionCodes.AiProviderDisable,
        PermissionCodes.AiProviderTest,
        PermissionCodes.AiCapabilityView,
        PermissionCodes.AiToolView,
        PermissionCodes.AiToolExecute,
        PermissionCodes.AiToolConfirmHighRisk,
        PermissionCodes.AiToolDataCenterView,
        PermissionCodes.AiToolDataCenterRead,
        PermissionCodes.AiToolDataCenterWrite,
        PermissionCodes.AiToolDataCenterOperate,
        PermissionCodes.AiLogView,
        PermissionCodes.AiUsageView
    ];
}

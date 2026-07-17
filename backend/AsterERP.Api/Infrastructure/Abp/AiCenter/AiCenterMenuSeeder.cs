using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Shared;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

internal static class AiCenterMenuSeeder
{
    public static void Seed(ISqlSugarClient db)
    {
        var tenantApps = db.Queryable<SystemTenantAppEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.Status == "Enabled" &&
                item.AppCode == "SYSTEM")
            .ToList();
        if (tenantApps.Count == 0)
        {
            return;
        }

        var tenantIds = tenantApps.Select(item => item.TenantId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var appCodes = tenantApps
            .Select(item => item.AppCode.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var menuCache = db.Queryable<SystemMenuEntity>()
            .Where(item => tenantIds.Contains(item.TenantId) && appCodes.Contains(item.AppCode))
            .ToList()
            .GroupBy(item => BuildMenuCacheKey(item.TenantId, item.AppCode, item.MenuCode), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var tenantApp in tenantApps)
        {
            var appCode = tenantApp.AppCode.Trim().ToUpperInvariant();
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai", "智能中心", null, null, null, "Directory", 20, true, null, "ph ph-sparkle");
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench", "AI 工作台", "ai", "/ai/workbench", "AiWorkbenchPage", "Menu", 1, true, PermissionCodes.AiWorkbenchView, "ph ph-chats-circle");
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability", "能力中心", "ai", "/ai/capability", "AiCapabilityCenterPage", "Menu", 2, true, PermissionCodes.AiCapabilityView, "ph ph-cube");
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:observability", "运行观测", "ai", "/ai/observability", "AiObservabilityPage", "Menu", 3, true, PermissionCodes.AiObservabilityView, "ph ph-chart-line-up");
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:security", "安全治理", "ai", "/ai/security", "AiSecurityPage", "Menu", 4, true, PermissionCodes.AiSecurityView, "ph ph-shield-check");
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:settings", "设置中心", "ai", "/ai/settings", "AiSettingsPage", "Menu", 5, true, PermissionCodes.AiSettingsView, "ph ph-gear-six");

            UpsertFlowiseMenus(db, menuCache, tenantApp.TenantId, appCode);

            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:conversation:create", "新建会话", "ai:workbench", null, null, "Button", 101, false, PermissionCodes.AiConversationCreate, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:conversation:edit", "编辑会话", "ai:workbench", null, null, "Button", 102, false, PermissionCodes.AiConversationEdit, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:conversation:archive", "归档会话", "ai:workbench", null, null, "Button", 103, false, PermissionCodes.AiConversationArchive, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:conversation:delete", "删除会话", "ai:workbench", null, null, "Button", 104, false, PermissionCodes.AiConversationDelete, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:task-plan:create", "新建计划", "ai:workbench", null, null, "Button", 111, false, PermissionCodes.AiTaskPlanCreate, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:task-plan:edit", "编辑计划", "ai:workbench", null, null, "Button", 112, false, PermissionCodes.AiTaskPlanEdit, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:task-plan:delete", "删除计划", "ai:workbench", null, null, "Button", 113, false, PermissionCodes.AiTaskPlanDelete, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:task-plan:approve", "批准计划", "ai:workbench", null, null, "Button", 114, false, PermissionCodes.AiTaskPlanApprove, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:task-plan:execute", "执行计划", "ai:workbench", null, null, "Button", 115, false, PermissionCodes.AiTaskPlanExecute, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:workflow:read", "Workflow 工具读取", "ai:workbench", null, null, "Button", 121, false, PermissionCodes.AiToolWorkflowRead, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:workflow:draft", "Workflow 草稿生成", "ai:workbench", null, null, "Button", 122, false, PermissionCodes.AiToolWorkflowDraft, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:workflow:validate", "Workflow 草稿校验", "ai:workbench", null, null, "Button", 123, false, PermissionCodes.AiToolWorkflowValidate, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:workflow:simulate", "Workflow 草稿模拟", "ai:workbench", null, null, "Button", 124, false, PermissionCodes.AiToolWorkflowSimulate, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:workflow:diagnose", "Workflow 诊断", "ai:workbench", null, null, "Button", 125, false, PermissionCodes.AiToolWorkflowDiagnose, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:workflow:publish-request", "发布前审查", "ai:workbench", null, null, "Button", 126, false, PermissionCodes.AiToolWorkflowPublishRequest, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:tool:confirm-high-risk", "确认高风险工具", "ai:workbench", null, null, "Button", 127, false, PermissionCodes.AiToolConfirmHighRisk, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:data-center:read", "数据中心工具读取", "ai:workbench", null, null, "Button", 131, false, PermissionCodes.AiToolDataCenterRead, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:data-center:write", "数据中心工具写入", "ai:workbench", null, null, "Button", 132, false, PermissionCodes.AiToolDataCenterWrite, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:workbench:data-center:operate", "数据中心工具操作", "ai:workbench", null, null, "Button", 133, false, PermissionCodes.AiToolDataCenterOperate, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:model:add", "新增模型", "ai:capability", null, null, "Button", 201, false, PermissionCodes.AiModelAdd, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:model:edit", "编辑模型", "ai:capability", null, null, "Button", 202, false, PermissionCodes.AiModelEdit, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:model:delete", "删除模型", "ai:capability", null, null, "Button", 203, false, PermissionCodes.AiModelDelete, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:model:disable", "停用模型", "ai:capability", null, null, "Button", 204, false, PermissionCodes.AiModelDisable, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:model:test", "测试模型", "ai:capability", null, null, "Button", 205, false, PermissionCodes.AiModelTest, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:model:copy", "复制模型", "ai:capability", null, null, "Button", 206, false, PermissionCodes.AiModelCopy, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:provider:add", "新增供应商", "ai:capability", null, null, "Button", 211, false, PermissionCodes.AiProviderAdd, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:provider:edit", "编辑供应商", "ai:capability", null, null, "Button", 212, false, PermissionCodes.AiProviderEdit, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:provider:delete", "删除供应商", "ai:capability", null, null, "Button", 213, false, PermissionCodes.AiProviderDelete, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:provider:disable", "停用供应商", "ai:capability", null, null, "Button", 214, false, PermissionCodes.AiProviderDisable, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:provider:test", "测试供应商", "ai:capability", null, null, "Button", 215, false, PermissionCodes.AiProviderTest, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:prompt:add", "新增提示词", "ai:capability", null, null, "Button", 221, false, PermissionCodes.AiPromptAdd, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:prompt:edit", "编辑提示词", "ai:capability", null, null, "Button", 222, false, PermissionCodes.AiPromptEdit, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:prompt:delete", "删除提示词", "ai:capability", null, null, "Button", 223, false, PermissionCodes.AiPromptDelete, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:prompt:copy", "复制提示词", "ai:capability", null, null, "Button", 224, false, PermissionCodes.AiPromptCopy, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:prompt:publish", "发布提示词", "ai:capability", null, null, "Button", 225, false, PermissionCodes.AiPromptPublish, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:prompt:test", "测试提示词", "ai:capability", null, null, "Button", 226, false, PermissionCodes.AiPromptTest, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:agent:add", "新增智能体", "ai:capability", null, null, "Button", 231, false, PermissionCodes.AiAgentAdd, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:agent:edit", "编辑智能体", "ai:capability", null, null, "Button", 232, false, PermissionCodes.AiAgentEdit, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:agent:delete", "删除智能体", "ai:capability", null, null, "Button", 233, false, PermissionCodes.AiAgentDelete, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:agent:copy", "复制智能体", "ai:capability", null, null, "Button", 234, false, PermissionCodes.AiAgentCopy, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:agent:disable", "停用智能体", "ai:capability", null, null, "Button", 235, false, PermissionCodes.AiAgentDisable, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:agent:test", "测试智能体", "ai:capability", null, null, "Button", 236, false, PermissionCodes.AiAgentTest, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:knowledge:upload", "上传知识文档", "ai:capability", null, null, "Button", 241, false, PermissionCodes.AiKnowledgeUpload, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:knowledge-graph:search", "知识图谱检索", "ai:capability", null, null, "Button", 242, false, PermissionCodes.AiKnowledgeGraphSearch, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:knowledge-graph:edit", "维护知识图谱", "ai:capability", null, null, "Button", 243, false, PermissionCodes.AiKnowledgeGraphEdit, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:knowledge-graph:reindex", "重建知识图谱", "ai:capability", null, null, "Button", 244, false, PermissionCodes.AiKnowledgeGraphReindex, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:knowledge-graph:import", "导入知识图谱", "ai:capability", null, null, "Button", 245, false, PermissionCodes.AiKnowledgeGraphImport, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:knowledge-graph:export", "导出知识图谱", "ai:capability", null, null, "Button", 246, false, PermissionCodes.AiKnowledgeGraphExport, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capability:tool:bind", "绑定工具", "ai:capability", null, null, "Button", 251, false, PermissionCodes.AiToolBindWorkflow, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:security:edit", "编辑安全策略", "ai:security", null, null, "Button", 401, false, PermissionCodes.AiSecurityEdit, null);
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:settings:edit", "编辑设置", "ai:settings", null, null, "Button", 501, false, PermissionCodes.AiSettingsEdit, null);

            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:chat", "AI 对话工作台", "ai", "/ai/chat", "AiWorkbenchPage", "Menu", 901, false, PermissionCodes.AiChatView, "ph ph-chats-circle");
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:conversations", "会话管理", "ai", "/ai/conversations", "AiWorkbenchPage", "Menu", 902, false, PermissionCodes.AiChatViewAll, "ph ph-chat-centered-dots");
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:prompt-templates", "提示词模板", "ai", "/ai/prompt-templates", "AiCapabilityCenterPage", "Menu", 903, false, PermissionCodes.AiPromptView, "ph ph-note-pencil");
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:model-configs", "模型配置", "ai", "/ai/model-configs", "AiCapabilityCenterPage", "Menu", 904, true, PermissionCodes.AiModelView, "ph ph-sliders-horizontal");
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:providers", "模型供应商", "ai", "/ai/providers", "AiCapabilityCenterPage", "Menu", 905, true, PermissionCodes.AiProviderView, "ph ph-cloud");
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:agents", "智能体配置", "ai", "/ai/agents", "AiCapabilityCenterPage", "Menu", 906, false, PermissionCodes.AiAgentView, "ph ph-users-three");
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:usage", "使用统计", "ai", "/ai/usage", "AiObservabilityPage", "Menu", 907, false, PermissionCodes.AiUsageView, "ph ph-chart-line-up");
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:logs", "调用日志", "ai", "/ai/logs", "AiObservabilityPage", "Menu", 908, false, PermissionCodes.AiLogView, "ph ph-list-magnifying-glass");
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:knowledge", "知识库", "ai", "/ai/knowledge", "AiCapabilityCenterPage", "Menu", 909, false, PermissionCodes.AiKnowledgeView, "ph ph-database");
            UpsertMenu(db, menuCache, tenantApp.TenantId, appCode, "ai:capabilities", "SK 能力矩阵", "ai", "/ai/sk-capabilities", "AiCapabilityCenterPage", "Menu", 910, false, PermissionCodes.AiCapabilityView, "ph ph-git-branch");
        }
    }

    private static void UpsertFlowiseMenus(ISqlSugarClient db, Dictionary<string, SystemMenuEntity> menuCache, string tenantId, string appCode)
    {
        UpsertMenu(db, menuCache, tenantId, appCode, "flowise", "Flowise", null, null, null, "Directory", 21, true, PermissionCodes.FlowiseView, "ph ph-node-tree");

        UpsertFlowiseLeaf(db, menuCache, tenantId, appCode, "flowise:chatflows", "Chatflows", "flowise", "/flowise/chatflows", "FlowiseChatflowsPage", 1, PermissionCodes.FlowiseChatflowsView, "ph ph-git-branch", PermissionCodes.FlowiseChatflowsEdit, PermissionCodes.FlowiseChatflowsRun);
        UpsertFlowiseFlowActionMenus(db, menuCache, tenantId, appCode, "flowise:chatflows", "Chatflows", PermissionCodes.FlowiseChatflowsEdit, PermissionCodes.FlowiseChatflowsDuplicate, PermissionCodes.FlowiseChatflowsExport, PermissionCodes.FlowiseTemplatesFlowExport, PermissionCodes.FlowiseChatflowsConfig, PermissionCodes.FlowiseChatflowsDomains, PermissionCodes.FlowiseChatflowsDelete);
        UpsertFlowiseLeaf(db, menuCache, tenantId, appCode, "flowise:agentflows", "Workflows", "flowise", "/flowise/agentflows", "FlowiseAgentflowsPage", 2, PermissionCodes.FlowiseAgentflowsView, "ph ph-robot", PermissionCodes.FlowiseAgentflowsEdit, PermissionCodes.FlowiseAgentflowsRun);
        UpsertFlowiseFlowActionMenus(db, menuCache, tenantId, appCode, "flowise:agentflows", "Workflows", PermissionCodes.FlowiseAgentflowsEdit, PermissionCodes.FlowiseAgentflowsDuplicate, PermissionCodes.FlowiseAgentflowsExport, PermissionCodes.FlowiseTemplatesFlowExport, PermissionCodes.FlowiseAgentflowsConfig, PermissionCodes.FlowiseAgentflowsDomains, PermissionCodes.FlowiseAgentflowsDelete);
        UpsertFlowiseLeaf(db, menuCache, tenantId, appCode, "flowise:executions", "Executions", "flowise", "/flowise/executions", "FlowiseExecutionsPage", 3, PermissionCodes.FlowiseExecutionsView, "ph ph-list-checks", PermissionCodes.FlowiseExecutionsManage);
        UpsertFlowiseLeaf(db, menuCache, tenantId, appCode, "flowise:assistants", "Assistants", "flowise", "/flowise/assistants", "FlowiseAssistantsPage", 4, PermissionCodes.FlowiseAssistantsView, "ph ph-user-sound", PermissionCodes.FlowiseAssistantsEdit);
        UpsertFlowiseLeaf(db, menuCache, tenantId, appCode, "flowise:marketplaces", "Marketplaces", "flowise", "/flowise/marketplaces", "FlowiseMarketplacesPage", 5, PermissionCodes.FlowiseMarketplacesView, "ph ph-storefront", PermissionCodes.FlowiseMarketplacesEdit);
        UpsertFlowiseLeaf(db, menuCache, tenantId, appCode, "flowise:tools", "Tools", "flowise", "/flowise/tools", "FlowiseToolsPage", 6, PermissionCodes.FlowiseToolsView, "ph ph-wrench", PermissionCodes.FlowiseToolsEdit);
        UpsertMenu(db, menuCache, tenantId, appCode, "flowise:tools:create", "Tools Create", "flowise:tools", null, null, "Button", 6, false, PermissionCodes.FlowiseToolsCreate, null);
        UpsertMenu(db, menuCache, tenantId, appCode, "flowise:tools:update", "Tools Update", "flowise:tools", null, null, "Button", 7, false, PermissionCodes.FlowiseToolsUpdate, null);
        UpsertMenu(db, menuCache, tenantId, appCode, "flowise:tools:delete", "Tools Delete", "flowise:tools", null, null, "Button", 8, false, PermissionCodes.FlowiseToolsDelete, null);
        UpsertFlowiseLeaf(db, menuCache, tenantId, appCode, "flowise:credentials", "Credentials", "flowise", "/flowise/credentials", "FlowiseCredentialsPage", 7, PermissionCodes.FlowiseCredentialsView, "ph ph-lock-key", PermissionCodes.FlowiseCredentialsEdit, null, PermissionCodes.FlowiseRevealSecret);
        UpsertFlowiseLeaf(db, menuCache, tenantId, appCode, "flowise:variables", "Variables", "flowise", "/flowise/variables", "FlowiseVariablesPage", 8, PermissionCodes.FlowiseVariablesView, "ph ph-brackets-curly", PermissionCodes.FlowiseVariablesEdit);
        UpsertFlowiseLeaf(db, menuCache, tenantId, appCode, "flowise:api-keys", "API Keys", "flowise", "/flowise/api-keys", "FlowiseApiKeysPage", 9, PermissionCodes.FlowiseApiKeysView, "ph ph-key", PermissionCodes.FlowiseApiKeysEdit, null, PermissionCodes.FlowiseRevealSecret);
        UpsertFlowiseLeaf(db, menuCache, tenantId, appCode, "flowise:document-stores", "Document Stores", "flowise", "/flowise/document-stores", "FlowiseDocumentStoresPage", 10, PermissionCodes.FlowiseDocumentStoresView, "ph ph-files", PermissionCodes.FlowiseDocumentStoresEdit);

        UpsertMenu(db, menuCache, tenantId, appCode, "flowise:evaluations-group", "Evaluations", "flowise", null, null, "Directory", 20, true, PermissionCodes.FlowiseEvaluationsView, "ph ph-chart-line-up");
        UpsertFlowiseLeaf(db, menuCache, tenantId, appCode, "flowise:datasets", "Datasets", "flowise:evaluations-group", "/flowise/datasets", "FlowiseDatasetsPage", 21, PermissionCodes.FlowiseDatasetsView, "ph ph-database", PermissionCodes.FlowiseDatasetsEdit);
        UpsertFlowiseLeaf(db, menuCache, tenantId, appCode, "flowise:evaluators", "Evaluators", "flowise:evaluations-group", "/flowise/evaluators", "FlowiseEvaluatorsPage", 22, PermissionCodes.FlowiseEvaluatorsView, "ph ph-test-tube", PermissionCodes.FlowiseEvaluatorsEdit);
        UpsertFlowiseLeaf(db, menuCache, tenantId, appCode, "flowise:evaluations", "Evaluations", "flowise:evaluations-group", "/flowise/evaluations", "FlowiseEvaluationsPage", 23, PermissionCodes.FlowiseEvaluationsView, "ph ph-chart-line-up", PermissionCodes.FlowiseEvaluationsEdit, PermissionCodes.FlowiseRun);

        UpsertMenu(db, menuCache, tenantId, appCode, "flowise:management-group", "User & Workspace Management", "flowise", null, null, "Directory", 30, true, PermissionCodes.FlowiseManage, "ph ph-users-three");
        UpsertFlowiseLeaf(db, menuCache, tenantId, appCode, "flowise:sso-config", "SSO Config", "flowise:management-group", "/flowise/sso-config", "FlowiseSsoConfigPage", 31, PermissionCodes.FlowiseSsoManage, "ph ph-shield-check", PermissionCodes.FlowiseSsoManage, null, PermissionCodes.FlowiseRevealSecret);
        UpsertFlowiseLeaf(db, menuCache, tenantId, appCode, "flowise:login-activity", "Login Activity", "flowise:management-group", "/flowise/login-activity", "FlowiseLoginActivityPage", 35, PermissionCodes.FlowiseLoginActivityView, "ph ph-clipboard-text", PermissionCodes.FlowiseLoginActivityManage);

        UpsertMenu(db, menuCache, tenantId, appCode, "flowise:others-group", "Others", "flowise", null, null, "Directory", 40, true, PermissionCodes.FlowiseView, "ph ph-dots-three-outline");
        UpsertFlowiseLeaf(db, menuCache, tenantId, appCode, "flowise:logs", "Logs", "flowise:others-group", "/flowise/logs", "FlowiseLogsPage", 41, PermissionCodes.FlowiseLogsView, "ph ph-list-magnifying-glass", PermissionCodes.FlowiseLogsManage);
        UpsertFlowiseLeaf(db, menuCache, tenantId, appCode, "flowise:account", "Account Settings", "flowise:others-group", "/flowise/account", "FlowiseAccountSettingsPage", 42, PermissionCodes.FlowiseAccountView, "ph ph-gear", PermissionCodes.FlowiseAccountEdit);
        RetireFlowisePlatformAccountMenus(db, tenantId, appCode);
    }

    private static void RetireFlowisePlatformAccountMenus(ISqlSugarClient db, string tenantId, string appCode)
    {
        var retiredMenuCodes = new[] { "flowise:roles", "flowise:users", "flowise:workspaces" };
        db.Updateable<SystemMenuEntity>()
            .SetColumns(item => new SystemMenuEntity
            {
                IsDeleted = true,
                Visible = false,
                UpdatedTime = DateTime.UtcNow
            })
            .Where(item =>
                item.TenantId == tenantId &&
                item.AppCode == appCode &&
                (retiredMenuCodes.Contains(item.MenuCode) || retiredMenuCodes.Contains(item.ParentCode!)))
            .ExecuteCommand();
    }

    private static void UpsertFlowiseLeaf(
        ISqlSugarClient db,
        Dictionary<string, SystemMenuEntity> menuCache,
        string tenantId,
        string appCode,
        string menuCode,
        string menuName,
        string parentCode,
        string routePath,
        string componentName,
        int sortOrder,
        string viewPermission,
        string icon,
        string? editPermission = null,
        string? runPermission = null,
        string? revealPermission = null)
    {
        UpsertMenu(db, menuCache, tenantId, appCode, menuCode, menuName, parentCode, routePath, componentName, "Menu", sortOrder, true, viewPermission, icon);
        if (!string.IsNullOrWhiteSpace(editPermission))
        {
            UpsertMenu(db, menuCache, tenantId, appCode, $"{menuCode}:edit", $"{menuName} Edit", menuCode, null, null, "Button", 1, false, editPermission, null);
            UpsertMenu(db, menuCache, tenantId, appCode, $"{menuCode}:import", $"{menuName} Import", menuCode, null, null, "Button", 2, false, PermissionCodes.FlowiseImport, null);
            UpsertMenu(db, menuCache, tenantId, appCode, $"{menuCode}:export", $"{menuName} Export", menuCode, null, null, "Button", 3, false, PermissionCodes.FlowiseExport, null);
        }

        if (!string.IsNullOrWhiteSpace(runPermission))
        {
            UpsertMenu(db, menuCache, tenantId, appCode, $"{menuCode}:run", $"{menuName} Run", menuCode, null, null, "Button", 4, false, runPermission, null);
        }

        if (!string.IsNullOrWhiteSpace(revealPermission))
        {
            UpsertMenu(db, menuCache, tenantId, appCode, $"{menuCode}:reveal", $"{menuName} Reveal", menuCode, null, null, "Button", 5, false, revealPermission, null);
        }
    }

    private static void UpsertFlowiseFlowActionMenus(
        ISqlSugarClient db,
        Dictionary<string, SystemMenuEntity> menuCache,
        string tenantId,
        string appCode,
        string menuCode,
        string menuName,
        string updatePermission,
        string duplicatePermission,
        string exportPermission,
        string templateExportPermission,
        string configPermission,
        string domainsPermission,
        string deletePermission)
    {
        UpsertMenu(db, menuCache, tenantId, appCode, $"{menuCode}:edit", $"{menuName} Rename", menuCode, null, null, "Button", 1, false, updatePermission, null);
        UpsertMenu(db, menuCache, tenantId, appCode, $"{menuCode}:duplicate", $"{menuName} Duplicate", menuCode, null, null, "Button", 2, false, duplicatePermission, null);
        UpsertMenu(db, menuCache, tenantId, appCode, $"{menuCode}:export", $"{menuName} Export", menuCode, null, null, "Button", 3, false, exportPermission, null);
        UpsertMenu(db, menuCache, tenantId, appCode, $"{menuCode}:template-export", $"{menuName} Save As Template", menuCode, null, null, "Button", 4, false, templateExportPermission, null);
        UpsertMenu(db, menuCache, tenantId, appCode, $"{menuCode}:starter-prompts", $"{menuName} Starter Prompts", menuCode, null, null, "Button", 5, false, configPermission, null);
        UpsertMenu(db, menuCache, tenantId, appCode, $"{menuCode}:chat-feedback", $"{menuName} Chat Feedback", menuCode, null, null, "Button", 6, false, configPermission, null);
        UpsertMenu(db, menuCache, tenantId, appCode, $"{menuCode}:domains", $"{menuName} Allowed Domains", menuCode, null, null, "Button", 7, false, domainsPermission, null);
        UpsertMenu(db, menuCache, tenantId, appCode, $"{menuCode}:speech-to-text", $"{menuName} Speech To Text", menuCode, null, null, "Button", 8, false, configPermission, null);
        UpsertMenu(db, menuCache, tenantId, appCode, $"{menuCode}:category", $"{menuName} Update Category", menuCode, null, null, "Button", 9, false, updatePermission, null);
        UpsertMenu(db, menuCache, tenantId, appCode, $"{menuCode}:delete", $"{menuName} Delete", menuCode, null, null, "Button", 10, false, deletePermission, null);
    }

    private static void UpsertMenu(
        ISqlSugarClient db,
        Dictionary<string, SystemMenuEntity> menuCache,
        string tenantId,
        string appCode,
        string menuCode,
        string menuName,
        string? parentCode,
        string? routePath,
        string? componentName,
        string menuType,
        int sortOrder,
        bool visible,
        string? permissionCode,
        string? icon)
    {
        var normalizedAppCode = appCode.Trim().ToUpperInvariant();
        var cacheKey = BuildMenuCacheKey(tenantId, normalizedAppCode, menuCode);
        menuCache.TryGetValue(cacheKey, out var existing);
        if (existing is null)
        {
            existing = new SystemMenuEntity
            {
                TenantId = tenantId,
                AppCode = normalizedAppCode,
                MenuCode = menuCode,
                MenuName = menuName,
                ParentCode = parentCode,
                RoutePath = routePath,
                ComponentName = componentName,
                MenuType = menuType,
                SortOrder = sortOrder,
                Visible = visible,
                PermissionCode = permissionCode,
                Icon = icon
            };
            db.Insertable(existing).ExecuteCommand();
            menuCache[cacheKey] = existing;
            return;
        }

        if (existing.MenuName == menuName &&
            existing.ParentCode == parentCode &&
            existing.RoutePath == routePath &&
            existing.ComponentName == componentName &&
            existing.MenuType == menuType &&
            existing.SortOrder == sortOrder &&
            existing.Visible == visible &&
            existing.PermissionCode == permissionCode &&
            existing.Icon == icon &&
            !existing.IsDeleted)
        {
            return;
        }

        existing.MenuName = menuName;
        existing.ParentCode = parentCode;
        existing.RoutePath = routePath;
        existing.ComponentName = componentName;
        existing.MenuType = menuType;
        existing.SortOrder = sortOrder;
        existing.Visible = visible;
        existing.PermissionCode = permissionCode;
        existing.Icon = icon;
        existing.IsDeleted = false;
        existing.UpdatedTime = DateTime.UtcNow;
        db.Updateable(existing).ExecuteCommand();
    }

    private static string BuildMenuCacheKey(string tenantId, string appCode, string menuCode)
        => $"{tenantId}::{appCode.Trim().ToUpperInvariant()}::{menuCode}";
}

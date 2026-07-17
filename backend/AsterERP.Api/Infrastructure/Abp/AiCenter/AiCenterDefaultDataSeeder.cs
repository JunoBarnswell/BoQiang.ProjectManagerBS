using AsterERP.Api.Modules.Ai;
using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Api.Modules.Platform;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

internal static class AiCenterDefaultDataSeeder
{
    public static void Seed(ISqlSugarClient db)
    {
        var tenantApps = db.Queryable<SystemTenantAppEntity>()
            .Where(item => !item.IsDeleted && item.Status == "Enabled")
            .ToList();

        foreach (var tenantApp in tenantApps)
        {
            var appCode = tenantApp.AppCode.Trim().ToUpperInvariant();
            UpsertSecurityPolicy(db, tenantApp.TenantId, appCode, "RequireToolConfirmation", "true", "工具类写操作必须二次确认");
            UpsertSecurityPolicy(db, tenantApp.TenantId, appCode, "MaxParallelAgents", "3", "单个协作运行最大并行智能体数");
            UpsertSecurityPolicy(db, tenantApp.TenantId, appCode, "MaxInputCharacters", "16000", "单次用户输入最大字符数");
            UpsertSecurityPolicy(db, tenantApp.TenantId, appCode, "MaxContextMessages", "40", "构建上下文时默认读取的最近消息数");
            UpsertSecurityPolicy(db, tenantApp.TenantId, appCode, "AllowReasoningDisplay", "true", "是否允许前端展示模型 reasoning_content");
            UpsertSecurityPolicy(db, tenantApp.TenantId, appCode, "MultiAgentFailurePolicy", "SkipFailed", "协作智能体失败时跳过失败者或整体失败");
            UpsertSystemSetting(db, tenantApp.TenantId, appCode, "DefaultProviderId", string.Empty, "String", "默认模型供应商");
            UpsertSystemSetting(db, tenantApp.TenantId, appCode, "DefaultModelConfigId", string.Empty, "String", "默认模型配置");
            UpsertSystemSetting(db, tenantApp.TenantId, appCode, "DefaultAgentProfileId", string.Empty, "String", "默认智能体");
            UpsertSystemSetting(db, tenantApp.TenantId, appCode, "DefaultPromptTemplateId", string.Empty, "String", "默认系统提示词");
            UpsertSystemSetting(db, tenantApp.TenantId, appCode, "NotificationSettingsJson", "{}", "Json", "智能中心通知设置");
            UpsertSystemSetting(db, tenantApp.TenantId, appCode, "LogRetentionDays", "180", "Number", "日志保留天数");
            UpsertSystemSetting(db, tenantApp.TenantId, appCode, "CleanupBatchSize", "500", "Number", "单次清理最大数量");
            UpsertDefaultPrompt(db, tenantApp.TenantId, appCode);
            UpsertDefaultAgent(db, tenantApp.TenantId, appCode);
            UpsertDefaultKnowledgeGraphTypes(db, tenantApp.TenantId, appCode);
            UpsertDefaultFlowiseWorkspace(db, tenantApp.TenantId, appCode);
            FlowiseOfficialDemoAgentflowSeed.Upsert(db, tenantApp.TenantId, appCode);
        }
    }

    private static void UpsertSecurityPolicy(ISqlSugarClient db, string tenantId, string appCode, string key, string value, string description)
    {
        var existing = db.Queryable<AiSecurityPolicyEntity>()
            .First(item => item.TenantId == tenantId && item.AppCode == appCode && item.PolicyKey == key);
        if (existing is null)
        {
            db.Insertable(new AiSecurityPolicyEntity
            {
                TenantId = tenantId,
                AppCode = appCode,
                PolicyKey = key,
                PolicyValue = value,
                Description = description,
                IsEnabled = true
            }).ExecuteCommand();
            return;
        }

        if (existing.PolicyValue == value &&
            existing.Description == description &&
            existing.IsEnabled &&
            !existing.IsDeleted)
        {
            return;
        }

        existing.PolicyValue = value;
        existing.Description = description;
        existing.IsEnabled = true;
        existing.IsDeleted = false;
        existing.UpdatedTime = DateTime.UtcNow;
        db.Updateable(existing).ExecuteCommand();
    }

    private static void UpsertSystemSetting(ISqlSugarClient db, string tenantId, string appCode, string key, string value, string valueType, string description)
    {
        var existing = db.Queryable<AiSystemSettingEntity>()
            .First(item => item.TenantId == tenantId && item.AppCode == appCode && item.SettingKey == key);
        if (existing is null)
        {
            db.Insertable(new AiSystemSettingEntity
            {
                TenantId = tenantId,
                AppCode = appCode,
                SettingKey = key,
                SettingValue = value,
                ValueType = valueType,
                Description = description
            }).ExecuteCommand();
            return;
        }

        if (existing.ValueType == valueType &&
            existing.Description == description &&
            !existing.IsDeleted)
        {
            return;
        }

        existing.ValueType = valueType;
        existing.Description = description;
        existing.IsDeleted = false;
        existing.UpdatedTime = DateTime.UtcNow;
        db.Updateable(existing).ExecuteCommand();
    }

    private static void UpsertDefaultFlowiseWorkspace(ISqlSugarClient db, string tenantId, string appCode)
    {
        const string workspaceKey = "default";
        var workspace = db.Queryable<FlowiseWorkspaceEntity>()
            .First(item => item.TenantId == tenantId && item.AppCode == appCode && item.WorkspaceKey == workspaceKey);
        if (workspace is null)
        {
            workspace = new FlowiseWorkspaceEntity
            {
                TenantId = tenantId,
                AppCode = appCode,
                OwnerUserId = "system",
                WorkspaceKey = workspaceKey,
                WorkspaceName = "Default Workspace",
                Status = "Enabled",
                Description = "Flowise Studio 默认工作区"
            };
            db.Insertable(workspace).ExecuteCommand();
        }
        else
        {
            var workspaceChanged = workspace.WorkspaceName != "Default Workspace" ||
                workspace.Status != "Enabled" ||
                workspace.IsDeleted;
            if (workspaceChanged)
            {
                workspace.WorkspaceName = "Default Workspace";
                workspace.Status = "Enabled";
                workspace.IsDeleted = false;
                workspace.UpdatedTime = DateTime.UtcNow;
                db.Updateable(workspace).ExecuteCommand();
            }
        }

        const string marketplaceKey = "starter-chatflow";
        var marketplace = db.Queryable<FlowiseMarketplaceTemplateEntity>()
            .First(item => item.TenantId == tenantId && item.AppCode == appCode && item.TemplateKey == marketplaceKey);
        if (marketplace is null)
        {
            db.Insertable(new FlowiseMarketplaceTemplateEntity
            {
                TenantId = tenantId,
                AppCode = appCode,
                OwnerUserId = "system",
                WorkspaceId = workspace.Id,
                TemplateKey = marketplaceKey,
                Name = "Starter Chatflow",
                Description = "AsterERP 原生 Flowise Studio 入门模板",
                Category = "Template",
                Status = "Enabled",
                FlowData = """{"nodes":[],"edges":[],"viewport":{"x":0,"y":0,"zoom":1},"template":{"nodes":["prompt","llm","output"],"runtime":"astererp"}}""",
                MetadataJson = """{"source":"system-seed"}"""
            }).ExecuteCommand();
            return;
        }

        const string starterFlowData = """{"nodes":[],"edges":[],"viewport":{"x":0,"y":0,"zoom":1},"template":{"nodes":["prompt","llm","output"],"runtime":"astererp"}}""";
        if (marketplace.WorkspaceId == workspace.Id &&
            marketplace.Name == "Starter Chatflow" &&
            marketplace.Description == "AsterERP 原生 Flowise Studio 入门模板" &&
            marketplace.Category == "Template" &&
            marketplace.Status == "Enabled" &&
            marketplace.FlowData == starterFlowData &&
            !marketplace.IsDeleted)
        {
            return;
        }

        marketplace.WorkspaceId = workspace.Id;
        marketplace.Name = "Starter Chatflow";
        marketplace.Description = "AsterERP 原生 Flowise Studio 入门模板";
        marketplace.Category = "Template";
        marketplace.Status = "Enabled";
        marketplace.FlowData = starterFlowData;
        marketplace.IsDeleted = false;
        marketplace.UpdatedTime = DateTime.UtcNow;
        db.Updateable(marketplace).ExecuteCommand();
    }

    private static void UpsertDefaultPrompt(ISqlSugarClient db, string tenantId, string appCode)
    {
        const string code = "default-assistant";
        var existing = db.Queryable<AiPromptTemplateEntity>()
            .First(item => item.TenantId == tenantId && item.AppCode == appCode && item.TemplateCode == code);
        if (existing is not null)
        {
            return;
        }

        db.Insertable(new AiPromptTemplateEntity
        {
            TenantId = tenantId,
            AppCode = appCode,
            TemplateCode = code,
            TemplateName = "通用业务助手",
            Category = "system",
            SystemPrompt = "你是 AsterERP / BigLogic 智能助手。回答要准确、可执行，涉及业务写操作时先说明影响并等待用户确认。",
            IsEnabled = true,
            SortOrder = 1
        }).ExecuteCommand();
    }

    private static void UpsertDefaultAgent(ISqlSugarClient db, string tenantId, string appCode)
    {
        const string code = "coordinator";
        var existing = db.Queryable<AiAgentProfileEntity>()
            .First(item => item.TenantId == tenantId && item.AppCode == appCode && item.AgentCode == code);
        if (existing is not null)
        {
            return;
        }

        db.Insertable(new AiAgentProfileEntity
        {
            TenantId = tenantId,
            AppCode = appCode,
            AgentCode = code,
            AgentName = "协调器",
            RolePrompt = "你负责汇总多个智能体的观点，输出清晰、可执行、可追踪的最终结论。",
            IsCoordinator = true,
            IsEnabled = true,
            SortOrder = 1
        }).ExecuteCommand();
    }

    private static void UpsertDefaultKnowledgeGraphTypes(ISqlSugarClient db, string tenantId, string appCode)
    {
        var nodeTypes = new[]
        {
            ("source", "知识来源", "知识库来源节点", "#2563eb", "database"),
            ("document", "知识文档", "知识库文档节点", "#059669", "file-text"),
            ("chunk", "文档片段", "文档分块节点", "#7c3aed", "braces"),
            ("term", "术语", "从文档片段抽取的确定性术语节点", "#dc2626", "tag")
        };

        foreach (var (code, name, description, color, icon) in nodeTypes)
        {
            var existing = db.Queryable<AiKnowledgeGraphNodeTypeEntity>()
                .First(item => item.TenantId == tenantId && item.AppCode == appCode && item.Code == code);
            if (existing is null)
            {
                db.Insertable(new AiKnowledgeGraphNodeTypeEntity
                {
                    TenantId = tenantId,
                    AppCode = appCode,
                    Code = code,
                    Name = name,
                    Description = description,
                    Color = color,
                    Icon = icon,
                    IsSystem = true
                }).ExecuteCommand();
                continue;
            }

            if (existing.Name == name &&
                existing.Description == description &&
                existing.Color == color &&
                existing.Icon == icon &&
                existing.IsSystem &&
                !existing.IsDeleted)
            {
                continue;
            }

            existing.Name = name;
            existing.Description = description;
            existing.Color = color;
            existing.Icon = icon;
            existing.IsSystem = true;
            existing.IsDeleted = false;
            existing.UpdatedTime = DateTime.UtcNow;
            db.Updateable(existing).ExecuteCommand();
        }

        var relationTypes = new[]
        {
            ("contains", "包含", true, "来源、文档、片段之间的层级包含关系", "#2563eb"),
            ("mentions", "提及", true, "片段提及术语或实体的关系", "#dc2626"),
            ("related", "相关", false, "人工维护的通用相关关系", "#64748b")
        };

        foreach (var (code, name, directional, description, color) in relationTypes)
        {
            var existing = db.Queryable<AiKnowledgeGraphRelationTypeEntity>()
                .First(item => item.TenantId == tenantId && item.AppCode == appCode && item.Code == code);
            if (existing is null)
            {
                db.Insertable(new AiKnowledgeGraphRelationTypeEntity
                {
                    TenantId = tenantId,
                    AppCode = appCode,
                    Code = code,
                    Name = name,
                    Directional = directional,
                    Description = description,
                    Color = color,
                    IsSystem = true
                }).ExecuteCommand();
                continue;
            }

            if (existing.Name == name &&
                existing.Directional == directional &&
                existing.Description == description &&
                existing.Color == color &&
                existing.IsSystem &&
                !existing.IsDeleted)
            {
                continue;
            }

            existing.Name = name;
            existing.Directional = directional;
            existing.Description = description;
            existing.Color = color;
            existing.IsSystem = true;
            existing.IsDeleted = false;
            existing.UpdatedTime = DateTime.UtcNow;
            db.Updateable(existing).ExecuteCommand();
        }
    }
}

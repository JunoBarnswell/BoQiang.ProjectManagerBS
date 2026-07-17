using AsterERP.Api.Application.Ai;
using AsterERP.Api.Application.Ai.Agent;
using AsterERP.Api.Application.Ai.Flowise;
using AsterERP.Api.Application.Ai.KnowledgeGraph;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.Ai;
using AsterERP.Api.Modules.Ai.Flowise;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

internal static class AiCenterDataFilterRegistrar
{
    public static void Register(IDataPermissionFilterRegistry registry)
    {
        var workspaceTypes = new[]
        {
            typeof(AiProviderEntity),
            typeof(AiModelConfigEntity),
            typeof(AiConversationEntity),
            typeof(AiMessageEntity),
            typeof(AiChatRunEntity),
            typeof(AiRunParticipantEntity),
            typeof(AiContextSnapshotEntity),
            typeof(AiPromptTemplateEntity),
            typeof(AiPromptVersionEntity),
            typeof(AiAgentProfileEntity),
            typeof(AiSystemSettingEntity),
            typeof(AiToolDefinitionEntity),
            typeof(AiToolBindingEntity),
            typeof(AiWorkflowToolBindingEntity),
            typeof(AiSecretRefEntity),
            typeof(AiToolExecutionLogEntity),
            typeof(AiUsageLogEntity),
            typeof(AiFeedbackEntity),
            typeof(AiTaskPlanEntity),
            typeof(AiTaskPlanItemEntity),
            typeof(AiTaskPlanEventEntity),
            typeof(AiTaskPlanItemOutputEntity),
            typeof(AiWorkflowDraftArtifactEntity),
            typeof(AiWorkflowValidationReportEntity),
            typeof(AiWorkflowSimulationReportEntity),
            typeof(AiWorkflowDiagnosisReportEntity),
            typeof(AiQuotaPolicyEntity),
            typeof(AiSecurityPolicyEntity),
            typeof(AiAuditEventEntity),
            typeof(AiSkCapabilityStatusEntity),
            typeof(AiKnowledgeSourceEntity),
            typeof(AiKnowledgeDocumentEntity),
            typeof(AiKnowledgeChunkEntity),
            typeof(AiKnowledgeIndexTaskEntity),
            typeof(AiKnowledgeGraphNodeTypeEntity),
            typeof(AiKnowledgeGraphRelationTypeEntity),
            typeof(AiKnowledgeGraphNodeEntity),
            typeof(AiKnowledgeGraphEdgeEntity),
            typeof(AiKnowledgeGraphEvidenceEntity),
            typeof(AiKnowledgeGraphBuildJobEntity),
            typeof(FlowiseWorkspaceEntity),
            typeof(FlowiseSharedWorkspaceEntity),
            typeof(FlowiseChatFlowEntity),
            typeof(FlowiseSsoConfigEntity),
            typeof(FlowiseRoleEntity),
            typeof(FlowiseUserEntity),
            typeof(FlowiseLoginActivityEntity),
            typeof(FlowiseAccountSettingEntity),
            typeof(FlowiseToolEntity),
            typeof(FlowiseCustomMcpServerEntity),
            typeof(FlowiseCredentialEntity),
            typeof(FlowiseVariableEntity),
            typeof(FlowiseApiKeyEntity),
            typeof(FlowiseAssistantEntity),
            typeof(FlowiseMarketplaceTemplateEntity),
            typeof(FlowiseDocumentStoreEntity),
            typeof(FlowiseDatasetEntity),
            typeof(FlowiseEvaluatorEntity),
            typeof(FlowiseEvaluationEntity),
            typeof(FlowiseExecutionEntity),
            typeof(FlowiseScheduleRecordEntity),
            typeof(FlowiseScheduleTriggerLogEntity),
            typeof(FlowiseAuditLogEntity),
            typeof(FlowiseNodeDefinitionEntity),
            typeof(FlowiseChatMessageEntity),
            typeof(FlowiseFeedbackEntity),
            typeof(FlowiseLeadEntity),
            typeof(FlowiseDocumentStoreFileEntity),
            typeof(FlowiseDocumentStoreChunkEntity),
            typeof(FlowiseVectorStoreConfigEntity),
            typeof(FlowiseDocumentStoreUpsertHistoryEntity),
            typeof(FlowiseDatasetRowEntity),
            typeof(FlowiseEvaluationResultEntity),
            typeof(AiTaskProcessStateEntity)
        };

        foreach (var type in workspaceTypes)
        {
            registry.RegisterAiWorkspaceFilter(type);
        }

        var ownedTypes = new[]
        {
            typeof(AiConversationEntity),
            typeof(AiMessageEntity),
            typeof(AiChatRunEntity),
            typeof(AiRunParticipantEntity),
            typeof(AiContextSnapshotEntity),
            typeof(AiToolExecutionLogEntity),
            typeof(AiFeedbackEntity),
            typeof(AiTaskPlanEntity),
            typeof(AiTaskPlanItemEntity),
            typeof(AiTaskPlanEventEntity),
            typeof(AiTaskPlanItemOutputEntity),
            typeof(AiWorkflowDraftArtifactEntity),
            typeof(AiWorkflowValidationReportEntity),
            typeof(AiWorkflowSimulationReportEntity),
            typeof(AiWorkflowDiagnosisReportEntity),
            typeof(AiKnowledgeSourceEntity),
            typeof(AiKnowledgeDocumentEntity),
            typeof(AiKnowledgeChunkEntity),
            typeof(AiKnowledgeIndexTaskEntity),
            typeof(AiKnowledgeGraphNodeEntity),
            typeof(AiKnowledgeGraphEdgeEntity),
            typeof(AiKnowledgeGraphEvidenceEntity),
            typeof(AiKnowledgeGraphBuildJobEntity),
            typeof(FlowiseWorkspaceEntity),
            typeof(FlowiseSharedWorkspaceEntity),
            typeof(FlowiseChatFlowEntity),
            typeof(FlowiseSsoConfigEntity),
            typeof(FlowiseRoleEntity),
            typeof(FlowiseUserEntity),
            typeof(FlowiseLoginActivityEntity),
            typeof(FlowiseAccountSettingEntity),
            typeof(FlowiseToolEntity),
            typeof(FlowiseCustomMcpServerEntity),
            typeof(FlowiseCredentialEntity),
            typeof(FlowiseVariableEntity),
            typeof(FlowiseApiKeyEntity),
            typeof(FlowiseAssistantEntity),
            typeof(FlowiseMarketplaceTemplateEntity),
            typeof(FlowiseDocumentStoreEntity),
            typeof(FlowiseDatasetEntity),
            typeof(FlowiseEvaluatorEntity),
            typeof(FlowiseEvaluationEntity),
            typeof(FlowiseExecutionEntity),
            typeof(FlowiseScheduleRecordEntity),
            typeof(FlowiseScheduleTriggerLogEntity),
            typeof(FlowiseAuditLogEntity),
            typeof(FlowiseNodeDefinitionEntity),
            typeof(FlowiseChatMessageEntity),
            typeof(FlowiseFeedbackEntity),
            typeof(FlowiseLeadEntity),
            typeof(FlowiseDocumentStoreFileEntity),
            typeof(FlowiseDocumentStoreChunkEntity),
            typeof(FlowiseVectorStoreConfigEntity),
            typeof(FlowiseDocumentStoreUpsertHistoryEntity),
            typeof(FlowiseDatasetRowEntity),
            typeof(FlowiseEvaluationResultEntity),
            typeof(AiTaskProcessStateEntity)
        };

        foreach (var type in ownedTypes)
        {
            registry.RegisterAiOwnedFilter(type);
        }
    }
}

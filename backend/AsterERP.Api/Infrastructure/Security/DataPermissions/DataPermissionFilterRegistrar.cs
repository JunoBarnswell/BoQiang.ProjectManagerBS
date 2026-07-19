using AsterERP.Shared;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Modules.AsterScene;
using AsterERP.Api.Modules.Ai;
using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using AsterERP.Api.Modules.Runtime;
using AsterERP.Api.Modules.System.Menus;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Api.Infrastructure.Abp.WorkflowApproval;
using AsterERP.Api.Infrastructure.Abp.AiCenter;
using AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;
using AsterERP.Api.Infrastructure.Abp.AsterScene;
using AsterERP.Api.Infrastructure.Abp.RuntimeCore;
using AsterERP.Api.Infrastructure.Abp.Im;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Modules.Im;
using AsterERP.Api.Modules.ProjectManagement;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public sealed class DataPermissionFilterRegistrar(
    AsterERP.Api.Infrastructure.Database.IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IHttpContextAccessor httpContextAccessor,
    DataPermissionRequestClassifier requestClassifier,
    IDataPermissionDescriptor<SystemUserEntity> systemUserDescriptor,
    IDataPermissionDescriptor<SystemRoleEntity> systemRoleDescriptor,
    IDataPermissionDescriptor<SystemMenuEntity> systemMenuDescriptor,
    IDataPermissionDescriptor<ApplicationDataSourceEntity> dataSourceDescriptor,
    IDataPermissionDescriptor<ApplicationQueryDatasetEntity> queryDatasetDescriptor,
    IDataPermissionDescriptor<ApplicationApiServiceEntity> apiServiceDescriptor,
    IDataPermissionDescriptor<ApplicationConnectionCheckTaskEntity> connectionCheckTaskDescriptor,
    IDataPermissionDescriptor<ApplicationConnectionCheckRunEntity> connectionCheckRunDescriptor,
    IDataPermissionDescriptor<ApplicationDataCenterDictionaryEntity> dictionaryDescriptor,
    IDataPermissionDescriptor<ApplicationDataEntityDefinitionEntity> entityDefinitionDescriptor,
    IDataPermissionDescriptor<ApplicationDataFieldDefinitionEntity> fieldDefinitionDescriptor,
    IDataPermissionDescriptor<ApplicationDataImportBatchEntity> importBatchDescriptor,
    IDataPermissionDescriptor<ApplicationDataModelDesignEntity> modelDesignDescriptor,
    IDataPermissionDescriptor<ApplicationDataObjectReferenceEntity> objectReferenceDescriptor,
    IDataPermissionDescriptor<ApplicationIntegrationTaskEntity> integrationTaskDescriptor,
    IDataPermissionDescriptor<ApplicationIntegrationTaskRunEntity> integrationTaskRunDescriptor,
    IDataPermissionDescriptor<ApplicationMicroflowEntity> microflowDescriptor,
    IDataPermissionDescriptor<ApplicationMicroflowRevisionEntity> microflowRevisionDescriptor,
    IDataPermissionDescriptor<ApplicationSqlScriptAuditEntity> sqlScriptAuditDescriptor,
    IDataPermissionDescriptor<ApplicationDataMutationLedgerEntity> mutationLedgerDescriptor,
    IDataPermissionDescriptor<ApplicationDataSourceCatalogSnapshotEntity> catalogSnapshotDescriptor,
    IDataPermissionDescriptor<ApplicationDataSourceSchemaChangePlanEntity> schemaChangePlanDescriptor,
    IDataPermissionDescriptor<ApplicationDataSourceSqlitePathApprovalEntity> sqlitePathApprovalDescriptor,
    IDataPermissionDescriptor<ApplicationDataSourceSqlitePathApprovalAuditEntity> sqlitePathApprovalAuditDescriptor,
    IDataPermissionDescriptor<ApplicationMappingCacheEntity> mappingCacheDescriptor,
    IDataPermissionDescriptor<ApplicationMappingCacheColumnEntity> mappingCacheColumnDescriptor,
    IDataPermissionDescriptor<ApplicationMappingCacheParameterEntity> mappingCacheParameterDescriptor) : IDataPermissionFilterRegistrar
{
    private ISqlSugarClient? registrationDb;

    private ISqlSugarClient Db => registrationDb ?? databaseAccessor.MainDb;

    public async Task<IDataPermissionFilterScope> RegisterAsync(CancellationToken cancellationToken = default)
    {
        var path = httpContextAccessor.HttpContext?.Request.Path.Value;
        if (requestClassifier.IsProjectManagementApi(path))
        {
            ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
        }

        registrationDb = ShouldUseWorkspaceDatabase()
            ? databaseAccessor.GetCurrentDb()
            : databaseAccessor.MainDb;
        Db.QueryFilter.ClearAndBackup();

        try
        {
            var registry = BuildRegistry();
            await RegisterCoreEntityFiltersAsync(cancellationToken);
            RegisterWorkspaceFilters(registry);
            RegisterProjectManagementFilters(registry);
            RegisterAiFilters(registry);
            RegisterWorkflowFilters(registry);
            RegisterImFilters(registry);
            AsterErpAsterSceneModule.RegisterDataFilters(registry);
            AsterErpRuntimeCoreModule.RegisterDataFilters(registry);
            return new SqlSugarDataPermissionFilterScope(Db);
        }
        catch
        {
            Db.QueryFilter.Restore();
            throw;
        }
    }

    private async Task RegisterCoreEntityFiltersAsync(CancellationToken cancellationToken)
    {
        var userFilter = await systemUserDescriptor.BuildAsync(cancellationToken);
        if (userFilter is not null)
        {
            Db.QueryFilter.AddTableFilter(userFilter);
        }

        var roleFilter = await systemRoleDescriptor.BuildAsync(cancellationToken);
        if (roleFilter is not null)
        {
            Db.QueryFilter.AddTableFilter(roleFilter);
        }

        var menuFilter = await systemMenuDescriptor.BuildAsync(cancellationToken);
        if (menuFilter is not null)
        {
            Db.QueryFilter.AddTableFilter(menuFilter);
        }

        var dataSourceFilter = await dataSourceDescriptor.BuildAsync(cancellationToken);
        if (dataSourceFilter is not null)
        {
            Db.QueryFilter.AddTableFilter(dataSourceFilter);
        }

        var queryDatasetFilter = await queryDatasetDescriptor.BuildAsync(cancellationToken);
        if (queryDatasetFilter is not null)
        {
            Db.QueryFilter.AddTableFilter(queryDatasetFilter);
        }

        await RegisterDescriptorFilterAsync(apiServiceDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(connectionCheckTaskDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(connectionCheckRunDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(dictionaryDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(entityDefinitionDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(fieldDefinitionDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(importBatchDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(modelDesignDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(objectReferenceDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(integrationTaskDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(integrationTaskRunDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(microflowDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(microflowRevisionDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(sqlScriptAuditDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(mutationLedgerDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(catalogSnapshotDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(schemaChangePlanDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(sqlitePathApprovalDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(sqlitePathApprovalAuditDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(mappingCacheDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(mappingCacheColumnDescriptor, cancellationToken);
        await RegisterDescriptorFilterAsync(mappingCacheParameterDescriptor, cancellationToken);
    }

    private async Task RegisterDescriptorFilterAsync<TEntity>(
        IDataPermissionDescriptor<TEntity> descriptor,
        CancellationToken cancellationToken)
    {
        var filter = await descriptor.BuildAsync(cancellationToken);
        if (filter is not null)
        {
            Db.QueryFilter.AddTableFilter(filter);
        }
    }

    private bool ShouldUseWorkspaceDatabase()
    {
        var path = httpContextAccessor.HttpContext?.Request.Path.Value ?? string.Empty;
        return requestClassifier.IsSystemAdministrationApi(path) ||
            requestClassifier.IsApplicationDevelopmentCenterApi(path) ||
            requestClassifier.IsApplicationDataCenterApi(path) ||
            requestClassifier.IsRuntimeWorkspaceApi(path) ||
            requestClassifier.IsAiWorkspaceApi(path) ||
            requestClassifier.IsWorkflowWorkspaceApi(path) ||
            requestClassifier.IsAsterSceneApi(path);
    }

    private DataPermissionFilterRegistry BuildRegistry()
    {
        var registry = new DataPermissionFilterRegistry();
        AsterErpWorkflowApprovalModule.RegisterDataFilters(registry);
        AsterErpAiCenterModule.RegisterDataFilters(registry);
        AsterErpApplicationDevelopmentCenterModule.RegisterDataFilters(registry);
        AsterErpImModule.RegisterDataFilters(registry);
        AsterErpProjectManagementModule.RegisterDataFilters(registry);
        registry.RegisterWorkspaceFilter(typeof(ApplicationDataCenterPublishedSnapshot));

        return registry;
    }

    private void RegisterWorkspaceFilters(DataPermissionFilterRegistry registry)
    {
        var path = httpContextAccessor.HttpContext?.Request.Path.Value ?? string.Empty;
        if ((!requestClassifier.IsApplicationDevelopmentCenterApi(path) &&
            !requestClassifier.IsRuntimeWorkspaceApi(path) &&
            !requestClassifier.IsApplicationDataCenterApi(path)) ||
            !currentUser.IsAsterErpAuthenticated())
        {
            return;
        }

        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
        {
            return;
        }

        appCode = appCode.Trim().ToUpperInvariant();
        foreach (var entityType in registry.WorkspaceEntityTypes)
        {
            if (entityType == typeof(ApplicationDataModelDesignEntity) ||
                entityType == typeof(ApplicationDataEntityDefinitionEntity) ||
                entityType == typeof(ApplicationDataFieldDefinitionEntity))
            {
                continue;
            }

            if (!ProjectManagementDataPermissionFilterRegistrar.TryRegister(Db, entityType, currentUser, tenantId, appCode))
            {
                RegisterWorkspaceFilter(entityType, tenantId, appCode);
            }
        }
    }

    private void RegisterProjectManagementFilters(DataPermissionFilterRegistry registry)
    {
        var path = httpContextAccessor.HttpContext?.Request.Path.Value ?? string.Empty;
        if (!requestClassifier.IsProjectManagementApi(path) ||
            !currentUser.IsAsterErpAuthenticated())
        {
            return;
        }

        var tenantId = currentUser.GetAsterErpTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return;
        }

        foreach (var entityType in registry.ProjectManagementEntityTypes)
        {
            if (!ProjectManagementDataPermissionFilterRegistrar.TryRegister(
                    Db,
                    entityType,
                    currentUser,
                    tenantId,
                    ProjectManagementPlatformScope.AppCode))
            {
                throw new InvalidOperationException($"Unsupported project-management data filter entity '{entityType.FullName}'.");
            }
        }
    }

    private void RegisterImFilters(DataPermissionFilterRegistry registry)
    {
        var path = httpContextAccessor.HttpContext?.Request.Path.Value ?? string.Empty;
        if (!requestClassifier.IsImApi(path) || !currentUser.IsAsterErpAuthenticated())
        {
            return;
        }

        var tenantId = currentUser.GetAsterErpTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return;
        }

        foreach (var entityType in registry.ImTenantEntityTypes)
        {
            RegisterImTenantFilter(entityType, tenantId);
        }
    }

    private void RegisterImTenantFilter(Type entityType, string tenantId)
    {
        if (entityType == typeof(ImAccountBindingEntity))
        {
            Db.QueryFilter.AddTableFilter<ImAccountBindingEntity>(item => item.TenantId == tenantId);
            return;
        }

        if (entityType == typeof(ImConversationEntity))
        {
            Db.QueryFilter.AddTableFilter<ImConversationEntity>(item => item.TenantId == tenantId);
            return;
        }

        if (entityType == typeof(ImConversationParticipantEntity))
        {
            Db.QueryFilter.AddTableFilter<ImConversationParticipantEntity>(item => item.TenantId == tenantId);
            return;
        }

        if (entityType == typeof(ImMessageEntity))
        {
            Db.QueryFilter.AddTableFilter<ImMessageEntity>(item => item.TenantId == tenantId);
            return;
        }

        if (entityType == typeof(ImMessageDeliveryLogEntity))
        {
            Db.QueryFilter.AddTableFilter<ImMessageDeliveryLogEntity>(item => item.TenantId == tenantId);
            return;
        }

        throw new InvalidOperationException($"Unsupported IM tenant data filter entity '{entityType.FullName}'.");
    }

    private void RegisterWorkspaceFilter(Type entityType, string tenantId, string appCode)
    {
        if (entityType == typeof(SystemDataModelEntity))
        {
            Db.QueryFilter.AddTableFilter<SystemDataModelEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(SystemTenantGridViewEntity))
        {
            Db.QueryFilter.AddTableFilter<SystemTenantGridViewEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(SystemUserGridViewEntity))
        {
            Db.QueryFilter.AddTableFilter<SystemUserGridViewEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationConnectionCheckTaskEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationConnectionCheckTaskEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationConnectionCheckRunEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationConnectionCheckRunEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationDataObjectReferenceEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationDataObjectReferenceEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationDataImportBatchEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationDataImportBatchEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationDevelopmentVersionEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationDevelopmentVersionEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationDevelopmentModuleEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationDevelopmentModuleEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationDevelopmentPageEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationDevelopmentPageEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationDesignerDocumentEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationDesignerDocumentEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationDesignerRevisionEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationDesignerRevisionEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationDesignerMigrationEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationDesignerMigrationEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationDesignerRuntimeArtifactEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationDesignerRuntimeArtifactEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationDesignerEditorSessionEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationDesignerEditorSessionEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationDesignerPublishRecordEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationDesignerPublishRecordEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationDesignerMigrationRunEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationDesignerMigrationRunEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationDesignerMigrationWatermarkEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationDesignerMigrationWatermarkEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationMonitoringEventEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationMonitoringEventEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationSharedResourceEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationSharedResourceEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationSqlScriptAuditEntity))
        {
            Db.QueryFilter.AddTableFilter<ApplicationSqlScriptAuditEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(ApplicationDataCenterPublishedSnapshot))
        {
            Db.QueryFilter.AddTableFilter<ApplicationDataCenterPublishedSnapshot>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        throw new InvalidOperationException($"Unsupported workspace data filter entity '{entityType.FullName}'.");
    }

    private void RegisterAiFilters(DataPermissionFilterRegistry registry)
    {
        var path = httpContextAccessor.HttpContext?.Request.Path.Value ?? string.Empty;
        if (!requestClassifier.IsAiWorkspaceApi(path) ||
            !currentUser.IsAsterErpAuthenticated())
        {
            return;
        }

        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
        {
            return;
        }

        appCode = appCode.Trim().ToUpperInvariant();
        foreach (var entityType in registry.AiWorkspaceEntityTypes)
        {
            RegisterAiWorkspaceFilter(entityType, tenantId, appCode);
        }

        if (currentUser.HasAsterErpPermission(PermissionCodes.AiChatViewAll))
        {
            return;
        }

        var currentUserId = currentUser.GetAsterErpUserId();
        foreach (var entityType in registry.AiOwnedEntityTypes)
        {
            RegisterAiOwnedFilter(entityType, tenantId, appCode, currentUserId);
        }
    }

    private void RegisterAiWorkspaceFilter(Type entityType, string tenantId, string appCode)
    {
        if (entityType == typeof(AiProviderEntity))
        {
            Db.QueryFilter.AddTableFilter<AiProviderEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiModelConfigEntity))
        {
            Db.QueryFilter.AddTableFilter<AiModelConfigEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiPromptTemplateEntity))
        {
            Db.QueryFilter.AddTableFilter<AiPromptTemplateEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiPromptVersionEntity))
        {
            Db.QueryFilter.AddTableFilter<AiPromptVersionEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiConversationEntity))
        {
            Db.QueryFilter.AddTableFilter<AiConversationEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiMessageEntity))
        {
            Db.QueryFilter.AddTableFilter<AiMessageEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiChatRunEntity))
        {
            Db.QueryFilter.AddTableFilter<AiChatRunEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiRunParticipantEntity))
        {
            Db.QueryFilter.AddTableFilter<AiRunParticipantEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiContextSnapshotEntity))
        {
            Db.QueryFilter.AddTableFilter<AiContextSnapshotEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiAgentProfileEntity))
        {
            Db.QueryFilter.AddTableFilter<AiAgentProfileEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiSystemSettingEntity))
        {
            Db.QueryFilter.AddTableFilter<AiSystemSettingEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiToolDefinitionEntity))
        {
            Db.QueryFilter.AddTableFilter<AiToolDefinitionEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiToolBindingEntity))
        {
            Db.QueryFilter.AddTableFilter<AiToolBindingEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiWorkflowToolBindingEntity))
        {
            Db.QueryFilter.AddTableFilter<AiWorkflowToolBindingEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiSecretRefEntity))
        {
            Db.QueryFilter.AddTableFilter<AiSecretRefEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiSecurityPolicyEntity))
        {
            Db.QueryFilter.AddTableFilter<AiSecurityPolicyEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiQuotaPolicyEntity))
        {
            Db.QueryFilter.AddTableFilter<AiQuotaPolicyEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiUsageLogEntity))
        {
            Db.QueryFilter.AddTableFilter<AiUsageLogEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiToolExecutionLogEntity))
        {
            Db.QueryFilter.AddTableFilter<AiToolExecutionLogEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiFeedbackEntity))
        {
            Db.QueryFilter.AddTableFilter<AiFeedbackEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiTaskPlanEntity))
        {
            Db.QueryFilter.AddTableFilter<AiTaskPlanEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiTaskPlanItemEntity))
        {
            Db.QueryFilter.AddTableFilter<AiTaskPlanItemEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiTaskPlanEventEntity))
        {
            Db.QueryFilter.AddTableFilter<AiTaskPlanEventEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiTaskPlanItemOutputEntity))
        {
            Db.QueryFilter.AddTableFilter<AiTaskPlanItemOutputEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiWorkflowDraftArtifactEntity))
        {
            Db.QueryFilter.AddTableFilter<AiWorkflowDraftArtifactEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiWorkflowValidationReportEntity))
        {
            Db.QueryFilter.AddTableFilter<AiWorkflowValidationReportEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiWorkflowSimulationReportEntity))
        {
            Db.QueryFilter.AddTableFilter<AiWorkflowSimulationReportEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiWorkflowDiagnosisReportEntity))
        {
            Db.QueryFilter.AddTableFilter<AiWorkflowDiagnosisReportEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiAuditEventEntity))
        {
            Db.QueryFilter.AddTableFilter<AiAuditEventEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiSkCapabilityStatusEntity))
        {
            Db.QueryFilter.AddTableFilter<AiSkCapabilityStatusEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiKnowledgeSourceEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeSourceEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiKnowledgeDocumentEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeDocumentEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiKnowledgeChunkEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeChunkEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiKnowledgeIndexTaskEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeIndexTaskEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiKnowledgeGraphNodeTypeEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeGraphNodeTypeEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiKnowledgeGraphRelationTypeEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeGraphRelationTypeEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiKnowledgeGraphNodeEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeGraphNodeEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiKnowledgeGraphEdgeEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeGraphEdgeEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiKnowledgeGraphEvidenceEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeGraphEvidenceEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiKnowledgeGraphBuildJobEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeGraphBuildJobEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseWorkspaceEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseWorkspaceEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseSsoConfigEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseSsoConfigEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseRoleEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseRoleEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseUserEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseUserEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseLoginActivityEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseLoginActivityEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseAccountSettingEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseAccountSettingEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseToolEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseToolEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseCustomMcpServerEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseCustomMcpServerEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseCredentialEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseCredentialEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseVariableEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseVariableEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseApiKeyEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseApiKeyEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseAssistantEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseAssistantEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseMarketplaceTemplateEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseMarketplaceTemplateEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseDocumentStoreEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseDocumentStoreEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseDatasetEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseDatasetEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseEvaluatorEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseEvaluatorEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseEvaluationEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseEvaluationEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseChatFlowEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseChatFlowEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseSharedWorkspaceEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseSharedWorkspaceEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseExecutionEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseExecutionEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseScheduleRecordEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseScheduleRecordEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseScheduleTriggerLogEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseScheduleTriggerLogEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseAuditLogEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseAuditLogEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseNodeDefinitionEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseNodeDefinitionEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseChatMessageEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseChatMessageEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseFeedbackEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseFeedbackEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseLeadEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseLeadEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseDocumentStoreFileEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseDocumentStoreFileEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseDocumentStoreChunkEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseDocumentStoreChunkEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseVectorStoreConfigEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseVectorStoreConfigEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseDocumentStoreUpsertHistoryEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseDocumentStoreUpsertHistoryEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseDatasetRowEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseDatasetRowEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(FlowiseEvaluationResultEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseEvaluationResultEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AiTaskProcessStateEntity))
        {
            Db.QueryFilter.AddTableFilter<AiTaskProcessStateEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        throw new InvalidOperationException($"Unsupported AI workspace data filter entity '{entityType.FullName}'.");
    }

    private void RegisterAiOwnedFilter(Type entityType, string tenantId, string appCode, string currentUserId)
    {
        if (typeof(IFlowiseSharedResourceEntity).IsAssignableFrom(entityType))
        {
            FlowiseDataPermissionFilter.Register(Db, entityType, tenantId, appCode, currentUserId);
            return;
        }

        if (entityType == typeof(AiConversationEntity))
        {
            Db.QueryFilter.AddTableFilter<AiConversationEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiMessageEntity))
        {
            Db.QueryFilter.AddTableFilter<AiMessageEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiChatRunEntity))
        {
            Db.QueryFilter.AddTableFilter<AiChatRunEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiRunParticipantEntity))
        {
            Db.QueryFilter.AddTableFilter<AiRunParticipantEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiContextSnapshotEntity))
        {
            Db.QueryFilter.AddTableFilter<AiContextSnapshotEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiToolExecutionLogEntity))
        {
            Db.QueryFilter.AddTableFilter<AiToolExecutionLogEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiFeedbackEntity))
        {
            Db.QueryFilter.AddTableFilter<AiFeedbackEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiTaskPlanEntity))
        {
            Db.QueryFilter.AddTableFilter<AiTaskPlanEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiTaskPlanItemEntity))
        {
            Db.QueryFilter.AddTableFilter<AiTaskPlanItemEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiTaskPlanEventEntity))
        {
            Db.QueryFilter.AddTableFilter<AiTaskPlanEventEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiTaskPlanItemOutputEntity))
        {
            Db.QueryFilter.AddTableFilter<AiTaskPlanItemOutputEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiWorkflowDraftArtifactEntity))
        {
            Db.QueryFilter.AddTableFilter<AiWorkflowDraftArtifactEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiWorkflowValidationReportEntity))
        {
            Db.QueryFilter.AddTableFilter<AiWorkflowValidationReportEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiWorkflowSimulationReportEntity))
        {
            Db.QueryFilter.AddTableFilter<AiWorkflowSimulationReportEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiWorkflowDiagnosisReportEntity))
        {
            Db.QueryFilter.AddTableFilter<AiWorkflowDiagnosisReportEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiKnowledgeSourceEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeSourceEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiKnowledgeDocumentEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeDocumentEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiKnowledgeChunkEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeChunkEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiKnowledgeIndexTaskEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeIndexTaskEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiKnowledgeGraphNodeEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeGraphNodeEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiKnowledgeGraphEdgeEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeGraphEdgeEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiKnowledgeGraphEvidenceEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeGraphEvidenceEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiKnowledgeGraphBuildJobEntity))
        {
            Db.QueryFilter.AddTableFilter<AiKnowledgeGraphBuildJobEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(FlowiseWorkspaceEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseWorkspaceEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(FlowiseSharedWorkspaceEntity))
        {
            Db.QueryFilter.AddTableFilter<FlowiseSharedWorkspaceEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(AiTaskProcessStateEntity))
        {
            Db.QueryFilter.AddTableFilter<AiTaskProcessStateEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        throw new InvalidOperationException($"Unsupported AI owned data filter entity '{entityType.FullName}'.");
    }

    private void RegisterWorkflowFilters(DataPermissionFilterRegistry registry)
    {
        var path = httpContextAccessor.HttpContext?.Request.Path.Value ?? string.Empty;
        if (!requestClassifier.IsWorkflowWorkspaceApi(path) ||
            !currentUser.IsAsterErpAuthenticated())
        {
            return;
        }

        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
        {
            return;
        }

        appCode = appCode.Trim().ToUpperInvariant();
        foreach (var entityType in registry.WorkflowWorkspaceEntityTypes)
        {
            RegisterWorkflowWorkspaceFilter(entityType, tenantId, appCode);
        }

        if (currentUser.HasAsterErpPermission("*"))
        {
            return;
        }

        var currentUserId = currentUser.GetAsterErpUserId();
        foreach (var entityType in registry.WorkflowOwnedEntityTypes)
        {
            RegisterWorkflowOwnedFilter(entityType, currentUserId);
        }
    }

    private void RegisterWorkflowWorkspaceFilter(Type entityType, string tenantId, string appCode)
    {
        if (entityType == typeof(WorkflowBindingEntity))
        {
            Db.QueryFilter.AddTableFilter<WorkflowBindingEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(WorkflowBusinessInstanceEntity))
        {
            Db.QueryFilter.AddTableFilter<WorkflowBusinessInstanceEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(WorkflowCallbackLogEntity))
        {
            Db.QueryFilter.AddTableFilter<WorkflowCallbackLogEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(WorkflowCategoryEntity))
        {
            Db.QueryFilter.AddTableFilter<WorkflowCategoryEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(WorkflowRequestDraftEntity))
        {
            Db.QueryFilter.AddTableFilter<WorkflowRequestDraftEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(WorkflowDelegationRuleEntity))
        {
            Db.QueryFilter.AddTableFilter<WorkflowDelegationRuleEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(WorkflowWorkCalendarEntity))
        {
            Db.QueryFilter.AddTableFilter<WorkflowWorkCalendarEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(WorkflowNotificationChannelEntity))
        {
            Db.QueryFilter.AddTableFilter<WorkflowNotificationChannelEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(WorkflowMessageTemplateEntity))
        {
            Db.QueryFilter.AddTableFilter<WorkflowMessageTemplateEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(WorkflowNodeNotificationRuleEntity))
        {
            Db.QueryFilter.AddTableFilter<WorkflowNodeNotificationRuleEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(WorkflowNotificationTaskEntity))
        {
            Db.QueryFilter.AddTableFilter<WorkflowNotificationTaskEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        throw new InvalidOperationException($"Unsupported workflow workspace data filter entity '{entityType.FullName}'.");
    }

    private void RegisterWorkflowOwnedFilter(Type entityType, string currentUserId)
    {
        if (entityType == typeof(WorkflowRequestDraftEntity))
        {
            Db.QueryFilter.AddTableFilter<WorkflowRequestDraftEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        if (entityType == typeof(WorkflowDelegationRuleEntity))
        {
            Db.QueryFilter.AddTableFilter<WorkflowDelegationRuleEntity>(item => item.OwnerUserId == currentUserId);
            return;
        }

        throw new InvalidOperationException($"Unsupported workflow owned data filter entity '{entityType.FullName}'.");
    }

    private void RegisterAsterSceneFilters(DataPermissionFilterRegistry registry)
    {
        var path = httpContextAccessor.HttpContext?.Request.Path.Value ?? string.Empty;
        if (requestClassifier.IsAsterScenePublicReadApi(path))
        {
            RegisterAsterScenePublicReadFilters(registry);
            return;
        }

        if (!requestClassifier.IsAsterSceneApi(path) || !currentUser.IsAsterErpAuthenticated())
        {
            return;
        }

        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
        {
            return;
        }

        appCode = appCode.Trim().ToUpperInvariant();
        var ownerUserId = currentUser.GetAsterErpUserId();
        foreach (var entityType in registry.AsterSceneWorkspaceEntityTypes)
        {
            RegisterAsterSceneWorkspaceFilter(entityType, tenantId, appCode, ownerUserId);
        }
    }

    private void RegisterAsterScenePublicReadFilters(DataPermissionFilterRegistry registry)
    {
        foreach (var entityType in registry.AsterSceneWorkspaceEntityTypes)
        {
            RegisterAsterScenePublicReadFilter(entityType);
        }
    }

    private void RegisterAsterScenePublicReadFilter(Type entityType)
    {
        if (entityType == typeof(AsterScenePublicWorkEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterScenePublicWorkEntity>(item =>
                !item.IsDeleted && item.Status == "Published" && item.Visibility != "Private");
            return;
        }

        if (entityType == typeof(AsterScenePublishVersionEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterScenePublishVersionEntity>(item =>
                !item.IsDeleted && item.Status == "Active" && item.Visibility != "Private");
            return;
        }

        if (entityType == typeof(AsterSceneCreatorProfileEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneCreatorProfileEntity>(item =>
                !item.IsDeleted && item.Status == "Active");
            return;
        }

        if (!currentUser.IsAsterErpAuthenticated())
        {
            return;
        }

        var userId = currentUser.GetAsterErpUserId();
        if (entityType == typeof(AsterSceneCommunityReactionEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneCommunityReactionEntity>(item =>
                !item.IsDeleted && item.UserId == userId);
            return;
        }

        if (entityType == typeof(AsterSceneRemixEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneRemixEntity>(item =>
                !item.IsDeleted && item.UserId == userId);
            return;
        }

        if (entityType == typeof(AsterSceneModerationCaseEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneModerationCaseEntity>(item =>
                !item.IsDeleted && item.ReporterUserId == userId);
        }
    }

    private void RegisterAsterSceneWorkspaceFilter(Type entityType, string tenantId, string appCode, string ownerUserId)
    {
        if (entityType == typeof(AsterSceneProjectEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneProjectEntity>(item => item.TenantId == tenantId && item.AppCode == appCode && item.OwnerUserId == ownerUserId);
            return;
        }

        if (entityType == typeof(AsterSceneDocumentEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneDocumentEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AsterSceneAssetEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneAssetEntity>(item => item.TenantId == tenantId && item.AppCode == appCode && item.OwnerUserId == ownerUserId);
            return;
        }

        if (entityType == typeof(AsterSceneAssetVersionEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneAssetVersionEntity>(item => item.TenantId == tenantId && item.AppCode == appCode && item.OwnerUserId == ownerUserId);
            return;
        }

        if (entityType == typeof(AsterSceneUploadSessionEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneUploadSessionEntity>(item => item.TenantId == tenantId && item.AppCode == appCode && item.OwnerUserId == ownerUserId);
            return;
        }

        if (entityType == typeof(AsterSceneUploadChunkEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneUploadChunkEntity>(item => item.TenantId == tenantId && item.AppCode == appCode && item.OwnerUserId == ownerUserId);
            return;
        }

        if (entityType == typeof(AsterSceneJobEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneJobEntity>(item => item.TenantId == tenantId && item.AppCode == appCode && item.OwnerUserId == ownerUserId);
            return;
        }

        if (entityType == typeof(AsterScenePublishVersionEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterScenePublishVersionEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AsterScenePublicWorkEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterScenePublicWorkEntity>(item =>
                item.TenantId == tenantId && item.AppCode == appCode && item.CreatorUserId == ownerUserId);
            return;
        }

        if (entityType == typeof(AsterSceneCreatorProfileEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneCreatorProfileEntity>(item =>
                item.TenantId == tenantId && item.AppCode == appCode && item.UserId == ownerUserId);
            return;
        }

        if (entityType == typeof(AsterSceneCommunityReactionEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneCommunityReactionEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AsterSceneRemixEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneRemixEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AsterSceneSubscriptionEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneSubscriptionEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AsterSceneUsageLedgerEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneUsageLedgerEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AsterSceneModerationCaseEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneModerationCaseEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AsterSceneAiCreditLedgerEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneAiCreditLedgerEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AsterSceneSupportTicketEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneSupportTicketEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AsterSceneSupportCommentEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneSupportCommentEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AsterSceneModerationDecisionEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneModerationDecisionEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AsterSceneModerationEvidenceEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneModerationEvidenceEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AsterSceneAppealEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneAppealEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        if (entityType == typeof(AsterSceneAppealDecisionEntity))
        {
            Db.QueryFilter.AddTableFilter<AsterSceneAppealDecisionEntity>(item => item.TenantId == tenantId && item.AppCode == appCode);
            return;
        }

        throw new InvalidOperationException($"Unsupported AsterScene workspace data filter entity '{entityType.FullName}'.");
    }

    private sealed class SqlSugarDataPermissionFilterScope(ISqlSugarClient db) : IDataPermissionFilterScope
    {
        public void Dispose()
        {
            db.QueryFilter.Restore();
        }
    }
}

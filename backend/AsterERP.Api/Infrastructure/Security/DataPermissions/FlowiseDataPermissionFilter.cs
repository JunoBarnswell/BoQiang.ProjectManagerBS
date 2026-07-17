using AsterERP.Api.Modules.Ai.Flowise;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

internal static class FlowiseDataPermissionFilter
{
    public static void Register(
        ISqlSugarClient db,
        Type entityType,
        string tenantId,
        string appCode,
        string currentUserId)
    {
        if (RegisterRelatedResourceFilter(db, entityType, tenantId, appCode, currentUserId))
        {
            return;
        }

        var registerMethod = typeof(FlowiseDataPermissionFilter)
            .GetMethod(nameof(RegisterRoot))?
            .MakeGenericMethod(entityType);
        registerMethod?.Invoke(null, [db, tenantId, appCode, currentUserId]);
    }

    private static void RegisterRoot<T>(
        ISqlSugarClient db,
        string tenantId,
        string appCode,
        string currentUserId)
        where T : class, IFlowiseSharedResourceEntity
    {
        db.QueryFilter.AddTableFilter<T>(item =>
            item.OwnerUserId == currentUserId ||
            SqlFunc.Subqueryable<FlowiseSharedWorkspaceEntity>()
                .Where(shared =>
                    !shared.IsDeleted &&
                    shared.TenantId == tenantId &&
                    shared.AppCode == appCode &&
                    shared.ItemId == item.Id &&
                    SqlFunc.Subqueryable<FlowiseWorkspaceEntity>()
                        .Where(workspace =>
                            !workspace.IsDeleted &&
                            workspace.TenantId == tenantId &&
                            workspace.AppCode == appCode &&
                            workspace.Id == shared.SharedWorkspaceId &&
                            workspace.OwnerUserId == currentUserId)
                        .Any())
                .Any());
    }

    private static bool RegisterRelatedResourceFilter(
        ISqlSugarClient db,
        Type entityType,
        string tenantId,
        string appCode,
        string currentUserId)
    {
        if (entityType == typeof(FlowiseChatMessageEntity))
        {
            db.QueryFilter.AddTableFilter<FlowiseChatMessageEntity>(item =>
                item.OwnerUserId == currentUserId ||
                SqlFunc.Subqueryable<FlowiseSharedWorkspaceEntity>()
                    .Where(shared =>
                        !shared.IsDeleted && shared.TenantId == tenantId && shared.AppCode == appCode &&
                        shared.ItemId == item.ResourceId &&
                        SqlFunc.Subqueryable<FlowiseWorkspaceEntity>()
                            .Where(workspace => !workspace.IsDeleted && workspace.TenantId == tenantId &&
                                workspace.AppCode == appCode && workspace.Id == shared.SharedWorkspaceId &&
                                workspace.OwnerUserId == currentUserId).Any()).Any());
            return true;
        }

        if (entityType == typeof(FlowiseExecutionEntity))
        {
            db.QueryFilter.AddTableFilter<FlowiseExecutionEntity>(item =>
                item.OwnerUserId == currentUserId ||
                SqlFunc.Subqueryable<FlowiseSharedWorkspaceEntity>()
                    .Where(shared => !shared.IsDeleted && shared.TenantId == tenantId && shared.AppCode == appCode &&
                        shared.ItemId == item.ResourceId &&
                        SqlFunc.Subqueryable<FlowiseWorkspaceEntity>()
                            .Where(workspace => !workspace.IsDeleted && workspace.TenantId == tenantId &&
                                workspace.AppCode == appCode && workspace.Id == shared.SharedWorkspaceId &&
                                workspace.OwnerUserId == currentUserId).Any()).Any());
            return true;
        }

        if (entityType == typeof(FlowiseLeadEntity))
        {
            db.QueryFilter.AddTableFilter<FlowiseLeadEntity>(item =>
                item.OwnerUserId == currentUserId ||
                SqlFunc.Subqueryable<FlowiseSharedWorkspaceEntity>()
                    .Where(shared => !shared.IsDeleted && shared.TenantId == tenantId && shared.AppCode == appCode &&
                        shared.ItemId == item.ResourceId &&
                        SqlFunc.Subqueryable<FlowiseWorkspaceEntity>()
                            .Where(workspace => !workspace.IsDeleted && workspace.TenantId == tenantId &&
                                workspace.AppCode == appCode && workspace.Id == shared.SharedWorkspaceId &&
                                workspace.OwnerUserId == currentUserId).Any()).Any());
            return true;
        }

        if (entityType == typeof(FlowiseDocumentStoreFileEntity))
        {
            db.QueryFilter.AddTableFilter<FlowiseDocumentStoreFileEntity>(item =>
                item.OwnerUserId == currentUserId ||
                SqlFunc.Subqueryable<FlowiseSharedWorkspaceEntity>()
                    .Where(shared => !shared.IsDeleted && shared.TenantId == tenantId && shared.AppCode == appCode &&
                        shared.ItemId == item.StoreId &&
                        SqlFunc.Subqueryable<FlowiseWorkspaceEntity>()
                            .Where(workspace => !workspace.IsDeleted && workspace.TenantId == tenantId &&
                                workspace.AppCode == appCode && workspace.Id == shared.SharedWorkspaceId &&
                                workspace.OwnerUserId == currentUserId).Any()).Any());
            return true;
        }

        if (entityType == typeof(FlowiseDocumentStoreChunkEntity))
        {
            db.QueryFilter.AddTableFilter<FlowiseDocumentStoreChunkEntity>(item =>
                item.OwnerUserId == currentUserId ||
                SqlFunc.Subqueryable<FlowiseSharedWorkspaceEntity>()
                    .Where(shared => !shared.IsDeleted && shared.TenantId == tenantId && shared.AppCode == appCode &&
                        shared.ItemId == item.StoreId &&
                        SqlFunc.Subqueryable<FlowiseWorkspaceEntity>()
                            .Where(workspace => !workspace.IsDeleted && workspace.TenantId == tenantId &&
                                workspace.AppCode == appCode && workspace.Id == shared.SharedWorkspaceId &&
                                workspace.OwnerUserId == currentUserId).Any()).Any());
            return true;
        }

        if (entityType == typeof(FlowiseDocumentStoreUpsertHistoryEntity))
        {
            db.QueryFilter.AddTableFilter<FlowiseDocumentStoreUpsertHistoryEntity>(item =>
                item.OwnerUserId == currentUserId ||
                SqlFunc.Subqueryable<FlowiseSharedWorkspaceEntity>()
                    .Where(shared => !shared.IsDeleted && shared.TenantId == tenantId && shared.AppCode == appCode &&
                        (shared.ItemId == item.StoreId || shared.ItemId == item.ChatflowId) &&
                        SqlFunc.Subqueryable<FlowiseWorkspaceEntity>()
                            .Where(workspace => !workspace.IsDeleted && workspace.TenantId == tenantId &&
                                workspace.AppCode == appCode && workspace.Id == shared.SharedWorkspaceId &&
                                workspace.OwnerUserId == currentUserId).Any()).Any());
            return true;
        }

        if (entityType == typeof(FlowiseDatasetRowEntity))
        {
            db.QueryFilter.AddTableFilter<FlowiseDatasetRowEntity>(item =>
                item.OwnerUserId == currentUserId ||
                SqlFunc.Subqueryable<FlowiseSharedWorkspaceEntity>()
                    .Where(shared => !shared.IsDeleted && shared.TenantId == tenantId && shared.AppCode == appCode &&
                        shared.ItemId == item.DatasetId &&
                        SqlFunc.Subqueryable<FlowiseWorkspaceEntity>()
                            .Where(workspace => !workspace.IsDeleted && workspace.TenantId == tenantId &&
                                workspace.AppCode == appCode && workspace.Id == shared.SharedWorkspaceId &&
                                workspace.OwnerUserId == currentUserId).Any()).Any());
            return true;
        }

        if (entityType == typeof(FlowiseEvaluationResultEntity))
        {
            db.QueryFilter.AddTableFilter<FlowiseEvaluationResultEntity>(item =>
                item.OwnerUserId == currentUserId ||
                SqlFunc.Subqueryable<FlowiseSharedWorkspaceEntity>()
                    .Where(shared => !shared.IsDeleted && shared.TenantId == tenantId && shared.AppCode == appCode &&
                        shared.ItemId == item.EvaluationId &&
                        SqlFunc.Subqueryable<FlowiseWorkspaceEntity>()
                            .Where(workspace => !workspace.IsDeleted && workspace.TenantId == tenantId &&
                                workspace.AppCode == appCode && workspace.Id == shared.SharedWorkspaceId &&
                                workspace.OwnerUserId == currentUserId).Any()).Any());
            return true;
        }

        if (entityType == typeof(FlowiseScheduleRecordEntity))
        {
            db.QueryFilter.AddTableFilter<FlowiseScheduleRecordEntity>(item =>
                item.OwnerUserId == currentUserId ||
                SqlFunc.Subqueryable<FlowiseSharedWorkspaceEntity>()
                    .Where(shared => !shared.IsDeleted && shared.TenantId == tenantId && shared.AppCode == appCode &&
                        shared.ItemId == item.TargetId &&
                        SqlFunc.Subqueryable<FlowiseWorkspaceEntity>()
                            .Where(workspace => !workspace.IsDeleted && workspace.TenantId == tenantId &&
                                workspace.AppCode == appCode && workspace.Id == shared.SharedWorkspaceId &&
                                workspace.OwnerUserId == currentUserId).Any()).Any());
            return true;
        }

        if (entityType == typeof(FlowiseScheduleTriggerLogEntity))
        {
            db.QueryFilter.AddTableFilter<FlowiseScheduleTriggerLogEntity>(item =>
                item.OwnerUserId == currentUserId ||
                SqlFunc.Subqueryable<FlowiseSharedWorkspaceEntity>()
                    .Where(shared => !shared.IsDeleted && shared.TenantId == tenantId && shared.AppCode == appCode &&
                        shared.ItemId == item.TargetId &&
                        SqlFunc.Subqueryable<FlowiseWorkspaceEntity>()
                            .Where(workspace => !workspace.IsDeleted && workspace.TenantId == tenantId &&
                                workspace.AppCode == appCode && workspace.Id == shared.SharedWorkspaceId &&
                                workspace.OwnerUserId == currentUserId).Any()).Any());
            return true;
        }

        if (entityType == typeof(FlowiseAuditLogEntity))
        {
            db.QueryFilter.AddTableFilter<FlowiseAuditLogEntity>(item =>
                item.OwnerUserId == currentUserId ||
                SqlFunc.Subqueryable<FlowiseSharedWorkspaceEntity>()
                    .Where(shared => !shared.IsDeleted && shared.TenantId == tenantId && shared.AppCode == appCode &&
                        shared.ItemId == item.ResourceId &&
                        SqlFunc.Subqueryable<FlowiseWorkspaceEntity>()
                            .Where(workspace => !workspace.IsDeleted && workspace.TenantId == tenantId &&
                                workspace.AppCode == appCode && workspace.Id == shared.SharedWorkspaceId &&
                                workspace.OwnerUserId == currentUserId).Any()).Any());
            return true;
        }

        if (entityType == typeof(FlowiseFeedbackEntity))
        {
            db.QueryFilter.AddTableFilter<FlowiseFeedbackEntity>(item =>
                item.OwnerUserId == currentUserId ||
                SqlFunc.Subqueryable<FlowiseChatMessageEntity>()
                    .Where(message => message.Id == item.MessageId &&
                        SqlFunc.Subqueryable<FlowiseSharedWorkspaceEntity>()
                            .Where(shared => !shared.IsDeleted && shared.TenantId == tenantId && shared.AppCode == appCode &&
                                shared.ItemId == message.ResourceId &&
                                SqlFunc.Subqueryable<FlowiseWorkspaceEntity>()
                                    .Where(workspace => !workspace.IsDeleted && workspace.TenantId == tenantId &&
                                        workspace.AppCode == appCode && workspace.Id == shared.SharedWorkspaceId &&
                                        workspace.OwnerUserId == currentUserId).Any()).Any()).Any());
            return true;
        }

        return false;
    }
}

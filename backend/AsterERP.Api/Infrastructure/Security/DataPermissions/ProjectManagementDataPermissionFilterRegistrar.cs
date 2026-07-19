using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Modules.ProjectManagement;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public static class ProjectManagementDataPermissionFilterRegistrar
{
    public static bool TryRegister(
        ISqlSugarClient db,
        Type entityType,
        ICurrentUser currentUser,
        string tenantId,
        string appCode)
    {
        appCode = ProjectManagementPlatformScope.AppCode;
        var userId = currentUser.GetAsterErpUserId();
        var restrictToMembership =
            !currentUser.IsAsterErpPlatformAdmin() &&
            !currentUser.HasAsterErpPermission("*") &&
            !string.Equals(currentUser.GetAsterErpDataScope(), "ALL", StringComparison.OrdinalIgnoreCase);
        var taskScopePredicate = ProjectManagementTaskScopeSqlPredicate.Create(tenantId, appCode, userId, restrictToMembership);

        if (entityType == typeof(ProjectManagementProjectEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementProjectEntity>(project =>
                project.TenantId == tenantId && project.AppCode == appCode &&
                (!restrictToMembership || project.OwnerUserId == userId ||
                 SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                     .Where(member => member.ProjectId == project.Id && member.TenantId == tenantId && member.AppCode == appCode && member.UserId == userId && member.IsActive && !member.IsDeleted)
                     .Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementProjectMemberEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementProjectMemberEntity>(member =>
                member.TenantId == tenantId && member.AppCode == appCode &&
                (!restrictToMembership || SqlFunc.Subqueryable<ProjectManagementProjectEntity>()
                    .Where(project => project.Id == member.ProjectId && project.TenantId == tenantId && project.AppCode == appCode &&
                        (project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                            .Where(projectMember => projectMember.ProjectId == project.Id && projectMember.TenantId == tenantId && projectMember.AppCode == appCode && projectMember.UserId == userId && projectMember.IsActive && !projectMember.IsDeleted)
                            .Any()))
                    .Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementMilestoneEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementMilestoneEntity>(milestone =>
                milestone.TenantId == tenantId && milestone.AppCode == appCode &&
                (!restrictToMembership || SqlFunc.Subqueryable<ProjectManagementProjectEntity>()
                    .Where(project => project.Id == milestone.ProjectId && project.TenantId == tenantId && project.AppCode == appCode &&
                        (project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                            .Where(projectMember => projectMember.ProjectId == project.Id && projectMember.TenantId == tenantId && projectMember.AppCode == appCode && projectMember.UserId == userId && projectMember.IsActive && !projectMember.IsDeleted)
                            .Any()))
                    .Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementTaskEntity))
        {
            db.QueryFilter.AddTableFilter(taskScopePredicate);
            return true;
        }

        if (entityType == typeof(ProjectManagementTaskDependencyEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementTaskDependencyEntity>(dependency =>
                dependency.TenantId == tenantId && dependency.AppCode == appCode &&
                (!restrictToMembership ||
                 SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(taskScopePredicate).Where(task => task.Id == dependency.PredecessorTaskId).Any() &&
                 SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(taskScopePredicate).Where(task => task.Id == dependency.SuccessorTaskId).Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementTaskParticipantEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementTaskParticipantEntity>(participant =>
                participant.TenantId == tenantId && participant.AppCode == appCode &&
                (!restrictToMembership || SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(taskScopePredicate).Where(task => task.Id == participant.TaskId).Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementLabelEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementLabelEntity>(label =>
                label.TenantId == tenantId && label.AppCode == appCode &&
                (!restrictToMembership || label.ProjectId == null || SqlFunc.Subqueryable<ProjectManagementProjectEntity>()
                    .Where(project => project.Id == label.ProjectId && (project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                        .Where(member => member.ProjectId == project.Id && member.UserId == userId && member.IsActive && !member.IsDeleted).Any())).Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementTaskLabelEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementTaskLabelEntity>(link =>
                link.TenantId == tenantId && link.AppCode == appCode &&
                (!restrictToMembership || SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(taskScopePredicate).Where(task => task.Id == link.TaskId).Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementTaskTimeLogEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementTaskTimeLogEntity>(log =>
                log.TenantId == tenantId && log.AppCode == appCode &&
                (!restrictToMembership || SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(taskScopePredicate).Where(task => task.Id == log.TaskId).Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementTaskTemplateEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementTaskTemplateEntity>(template => template.TenantId == tenantId && template.AppCode == appCode && (!restrictToMembership || template.ProjectId == null || SqlFunc.Subqueryable<ProjectManagementProjectEntity>().Where(project => project.Id == template.ProjectId && (project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>().Where(member => member.ProjectId == project.Id && member.UserId == userId && member.IsActive && !member.IsDeleted).Any())).Any()));
            return true;
        }
        if (entityType == typeof(ProjectManagementTaskOccurrenceEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementTaskOccurrenceEntity>(occurrence => occurrence.TenantId == tenantId && occurrence.AppCode == appCode && (!restrictToMembership || SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(taskScopePredicate).Where(task => task.Id == occurrence.RootTaskId).Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementTaskRecurrenceEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementTaskRecurrenceEntity>(recurrence =>
                recurrence.TenantId == tenantId && recurrence.AppCode == appCode &&
                (!restrictToMembership || SqlFunc.Subqueryable<ProjectManagementTaskEntity>()
                    .Where(taskScopePredicate)
                    .Where(task => task.Id == recurrence.SourceTaskId)
                    .Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementTaskRecurrenceOccurrenceEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementTaskRecurrenceOccurrenceEntity>(occurrence =>
                occurrence.TenantId == tenantId && occurrence.AppCode == appCode &&
                (!restrictToMembership || SqlFunc.Subqueryable<ProjectManagementTaskEntity>()
                    .Where(taskScopePredicate)
                    .Where(task => task.Id == occurrence.TaskId)
                    .Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementActivityEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementActivityEntity>(activity => activity.TenantId == tenantId && activity.AppCode == appCode && (!restrictToMembership || SqlFunc.Subqueryable<ProjectManagementProjectEntity>().Where(project => project.Id == activity.ProjectId && (project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>().Where(member => member.ProjectId == project.Id && member.UserId == userId && member.IsActive && !member.IsDeleted).Any())).Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementTaskCommentEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementTaskCommentEntity>(comment => comment.TenantId == tenantId && comment.AppCode == appCode && (!restrictToMembership || SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(taskScopePredicate).Where(task => task.Id == comment.TaskId).Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementTaskCommentMentionEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementTaskCommentMentionEntity>(mention => mention.TenantId == tenantId && mention.AppCode == appCode && (!restrictToMembership || SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(taskScopePredicate).Where(task => task.Id == mention.TaskId).Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementNotificationEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementNotificationEntity>(notification => notification.TenantId == tenantId && notification.AppCode == appCode && notification.RecipientUserId == userId);
            return true;
        }

        if (entityType == typeof(ProjectManagementTaskReminderEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementTaskReminderEntity>(reminder => reminder.TenantId == tenantId && reminder.AppCode == appCode && (!restrictToMembership || SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(taskScopePredicate).Where(task => task.Id == reminder.TaskId).Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementSavedViewEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementSavedViewEntity>(view => view.TenantId == tenantId && view.AppCode == appCode && (!restrictToMembership || view.OwnerUserId == userId || (view.IsShared && SqlFunc.Subqueryable<ProjectManagementProjectEntity>().Where(project => project.Id == view.ProjectId && (project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>().Where(member => member.ProjectId == project.Id && member.UserId == userId && member.IsActive && !member.IsDeleted).Any())).Any())));
            return true;
        }

        if (entityType == typeof(ProjectManagementTaskAttachmentEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementTaskAttachmentEntity>(attachment => attachment.TenantId == tenantId && attachment.AppCode == appCode && (!restrictToMembership || SqlFunc.Subqueryable<ProjectManagementTaskEntity>().Where(taskScopePredicate).Where(task => task.Id == attachment.TaskId).Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementExternalApiRequestEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementExternalApiRequestEntity>(request =>
                request.TenantId == tenantId && request.AppCode == appCode &&
                (!restrictToMembership || request.CallerUserId == userId));
            return true;
        }

        if (entityType == typeof(ProjectManagementImConversationLinkEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementImConversationLinkEntity>(link =>
                link.TenantId == tenantId && link.AppCode == appCode &&
                (!restrictToMembership || SqlFunc.Subqueryable<ProjectManagementProjectEntity>()
                    .Where(project => project.Id == link.ProjectId && project.TenantId == tenantId && project.AppCode == appCode &&
                        (project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                            .Where(member => member.ProjectId == project.Id && member.TenantId == tenantId && member.AppCode == appCode && member.UserId == userId && member.IsActive && !member.IsDeleted)
                            .Any()))
                    .Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementSyncJournalEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementSyncJournalEntity>(journal => journal.TenantId == tenantId && journal.AppCode == appCode && (!restrictToMembership || journal.ProjectId == null || SqlFunc.Subqueryable<ProjectManagementProjectEntity>().Where(project => project.Id == journal.ProjectId && (project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>().Where(member => member.ProjectId == project.Id && member.UserId == userId && member.IsActive && !member.IsDeleted).Any())).Any()));
            return true;
        }

        if (entityType == typeof(ProjectManagementSyncDeviceEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementSyncDeviceEntity>(device => device.TenantId == tenantId && device.AppCode == appCode);
            return true;
        }

        if (entityType == typeof(ProjectManagementMaintenanceLockEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementMaintenanceLockEntity>(lockEntity => lockEntity.TenantId == tenantId && lockEntity.AppCode == appCode);
            return true;
        }

        if (entityType == typeof(ProjectManagementBackupEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementBackupEntity>(backup => backup.TenantId == tenantId && backup.AppCode == appCode);
            return true;
        }

        if (entityType == typeof(ProjectManagementOperationEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementOperationEntity>(operation => operation.TenantId == tenantId && operation.AppCode == appCode);
            return true;
        }

        if (entityType == typeof(ProjectManagementReversibleCommandEntity))
        {
            db.QueryFilter.AddTableFilter<ProjectManagementReversibleCommandEntity>(command => command.TenantId == tenantId && command.AppCode == appCode);
            return true;
        }

        return false;
    }
}

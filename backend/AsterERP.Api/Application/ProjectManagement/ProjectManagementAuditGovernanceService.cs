using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Abp.Settings;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Settings;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementAuditGovernanceService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementOperationWriter operationWriter,
    IBackgroundJobManager backgroundJobManager,
    ISettingProvider? settingProvider = null) : IProjectManagementAuditGovernanceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ProjectManagementAuditGovernancePolicy> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        RequireManagePermission();
        return await LoadPolicyAsync(cancellationToken);
    }

    public async Task<ProjectManagementOperationResponse> StartCleanupAsync(CancellationToken cancellationToken = default)
    {
        RequireManagePermission();
        var policy = await LoadPolicyAsync(cancellationToken);
        var operationId = Guid.NewGuid().ToString("N");
        var traceId = global::System.Diagnostics.Activity.Current?.Id ?? operationId;
        var impact = new GovernanceImpact(policy, 0, 0, 0, 0, false, null);
        await operationWriter.CreatePendingAsync(operationId, "audit.governance.cleanup", JsonSerializer.Serialize(impact, JsonOptions), traceId, cancellationToken);
        try
        {
            await backgroundJobManager.EnqueueAsync(new ProjectManagementOperationJobArgs(operationId, Tenant(), App(), UserId(), traceId));
        }
        catch (Exception exception)
        {
            await operationWriter.FailAsync(operationId, $"审计治理任务入队失败：{exception.Message}", CancellationToken.None);
            throw;
        }
        return await GetOwnedAsync(operationId, cancellationToken);
    }

    public async Task ExecuteCleanupAsync(string operationId, CancellationToken cancellationToken = default)
    {
        var operation = await GetOwnedEntityAsync(operationId, cancellationToken);
        var impact = DeserializeImpact(operation.ImpactJson);
        try
        {
            await operationWriter.StartAsync(operation.Id, "audit.governance.cleanup", operation.ImpactJson, operation.TraceId, cancellationToken);
            if (!await operationWriter.ReportProgressAsync(operation.Id, "正在扫描审计保留边界", 15, cancellationToken)) return;
            var db = databaseAccessor.GetProjectManagementDb();
            var now = DateTime.UtcNow;
            var activeCutoff = now.AddDays(-impact.Policy.ActiveRetentionDays);
            var archiveCutoff = now.AddDays(-impact.Policy.ArchiveRetentionDays);
            var candidates = await db.Queryable<ProjectManagementActivityEntity>()
                .Where(item => item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted && item.CreatedTime < activeCutoff)
                .OrderBy(item => item.CreatedTime, OrderByType.Asc)
                .Take(impact.Policy.CleanupBatchSize)
                .ToListAsync(cancellationToken);
            var highRiskPreserved = candidates.Count(IsHighRisk);
            var archiveIds = candidates.Where(item => item.ArchivedTime is null && !IsHighRisk(item)).Select(item => item.Id).ToArray();
            if (archiveIds.Length > 0)
            {
                await db.Updateable<ProjectManagementActivityEntity>()
                    .SetColumns(item => new ProjectManagementActivityEntity { ArchivedTime = now, ActorUserId = "[已归档]", Remark = null, UpdatedBy = UserId(), UpdatedTime = now })
                    .Where(item => archiveIds.Contains(item.Id) && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted)
                    .ExecuteCommandAsync(cancellationToken);
            }

            if (!await operationWriter.ReportProgressAsync(operation.Id, "正在清理超过归档保留期的一般活动", 55, cancellationToken)) return;
            var expiredArchived = await db.Queryable<ProjectManagementActivityEntity>()
                .Where(item => item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted && item.ArchivedTime != null && item.ArchivedTime < archiveCutoff)
                .OrderBy(item => item.ArchivedTime, OrderByType.Asc)
                .Take(impact.Policy.CleanupBatchSize)
                .ToListAsync(cancellationToken);
            var deleteIds = expiredArchived.Where(item => !IsHighRisk(item)).Select(item => item.Id).ToArray();
            if (deleteIds.Length > 0)
            {
                await db.Updateable<ProjectManagementActivityEntity>()
                    .SetColumns(item => new ProjectManagementActivityEntity { IsDeleted = true, DeletedBy = UserId(), DeletedTime = now, UpdatedBy = UserId(), UpdatedTime = now })
                    .Where(item => deleteIds.Contains(item.Id) && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted)
                    .ExecuteCommandAsync(cancellationToken);
            }

            var activityCount = await db.Queryable<ProjectManagementActivityEntity>().Where(item => item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted).CountAsync(cancellationToken);
            var operationCount = await db.Queryable<ProjectManagementOperationEntity>().Where(item => item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted).CountAsync(cancellationToken);
            var capacityAlert = activityCount + operationCount >= impact.Policy.CapacityLimit;
            var completed = impact with { ArchivedCount = archiveIds.Length, DeletedCount = deleteIds.Length, HighRiskPreservedCount = highRiskPreserved, TotalAuditCount = activityCount + operationCount, CapacityAlert = capacityAlert, CompletedAt = now };
            await operationWriter.CompleteWithImpactAsync(operation.Id, JsonSerializer.Serialize(completed, JsonOptions), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await operationWriter.FailAsync(operation.Id, $"审计治理清理失败：{exception.Message}", CancellationToken.None);
        }
    }

    private async Task<ProjectManagementAuditGovernancePolicy> LoadPolicyAsync(CancellationToken cancellationToken)
    {
        var provider = settingProvider;
        if (provider is null) return new();
        var activeDays = await ReadIntAsync(provider, AsterErpSettingNames.ProjectManagementAuditActiveRetentionDays, 180, 1, 3650, cancellationToken);
        var archiveDays = await ReadIntAsync(provider, AsterErpSettingNames.ProjectManagementAuditArchiveRetentionDays, 2555, 2, 36500, cancellationToken);
        return new(
            activeDays,
            Math.Max(archiveDays, activeDays + 1),
            await ReadIntAsync(provider, AsterErpSettingNames.ProjectManagementAuditCleanupBatchSize, 1000, 100, 10000, cancellationToken),
            await ReadIntAsync(provider, AsterErpSettingNames.ProjectManagementAuditCapacityLimit, 100000, 1000, 10000000, cancellationToken));
    }

    private static async Task<int> ReadIntAsync(ISettingProvider provider, string name, int fallback, int minimum, int maximum, CancellationToken cancellationToken)
    {
        var value = await provider.GetOrNullAsync(name);
        return int.TryParse(value, out var parsed) ? Math.Clamp(parsed, minimum, maximum) : fallback;
    }

    private static bool IsHighRisk(ProjectManagementActivityEntity item)
    {
        var value = $"{item.ActivityType} {item.Summary}";
        return new[] { "security", "permission", "login", "backup", "restore", "import", "export", "purge", "delete", "recovery", "approval", "workflow", "sync", "governance", "失败" }
            .Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static GovernanceImpact DeserializeImpact(string json) => JsonSerializer.Deserialize<GovernanceImpact>(json, JsonOptions) ?? throw new ValidationException("审计治理任务数据损坏");

    private async Task<ProjectManagementOperationResponse> GetOwnedAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await GetOwnedEntityAsync(id, cancellationToken);
        return new(entity.Id, entity.OperationType, entity.Status, entity.Phase, entity.ProgressPercent, entity.IsCancellationRequested, entity.ImpactJson, entity.ErrorMessage, entity.TraceId, entity.StartedTime, entity.CompletedTime);
    }

    private async Task<ProjectManagementOperationEntity> GetOwnedEntityAsync(string id, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.Id == id.Trim() && item.TenantId == Tenant() && item.AppCode == App() && item.ActorUserId == UserId() && item.OperationType == "audit.governance.cleanup" && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
        ?? throw new NotFoundException("审计治理任务不存在或无权访问", ErrorCodes.PlatformResourceNotFound);

    private void RequireManagePermission()
    {
        ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
        if (!currentUser.HasAsterErpPermission(PermissionCodes.ProjectManagementOperationManage)) throw new ValidationException("无权执行审计治理", ErrorCodes.PermissionDenied);
    }

    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string UserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);

    private sealed record GovernanceImpact(ProjectManagementAuditGovernancePolicy Policy, int ArchivedCount, int DeletedCount, int HighRiskPreservedCount, int TotalAuditCount, bool CapacityAlert, DateTime? CompletedAt);
}

using System.Text;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementSyncHistoryService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser) : IProjectManagementSyncHistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RecordAsync(ProjectManagementSyncHistoryRecord record, CancellationToken cancellationToken = default)
    {
        var result = record.Result;
        var now = DateTime.UtcNow;
        await databaseAccessor.GetCurrentDb().Insertable(new ProjectManagementSyncHistoryEntity
        {
            Id = result.ImportId,
            TenantId = Tenant(), AppCode = App(), OperationType = record.OperationType,
            PackageId = Required(record.PackageId, "同步包标识不能为空"),
            SourceTenantId = Required(record.SourceTenantId, "同步来源租户不能为空"),
            SourceAppCode = Required(record.SourceAppCode, "同步来源应用不能为空"),
            SourceDeviceId = Optional(record.SourceDeviceId),
            TargetTenantId = Tenant(), TargetAppCode = App(), ActorUserId = User(), Status = Required(record.Status, "同步状态不能为空"),
            Inserted = result.Inserted, Updated = result.Updated, Deleted = result.Deleted, Skipped = result.Skipped,
            ConflictCount = result.ConflictCount, Failed = result.Failed, AttachmentsImported = result.AttachmentsImported,
            Strategy = result.Strategy, ReportJson = JsonSerializer.Serialize(new SyncHistoryReport(result.Warnings, result.Conflicts ?? []), JsonOptions),
            ErrorMessage = Optional(record.ErrorMessage), RetryOfHistoryId = Optional(record.RetryOfHistoryId), TraceId = result.TraceId,
            OccurredAt = now, CreatedBy = User(), CreatedTime = now
        }).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ProjectManagementSyncHistoryPage> QueryAsync(ProjectManagementSyncHistoryQuery query, CancellationToken cancellationToken = default)
    {
        var pageIndex = Math.Max(1, query.PageIndex);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var db = databaseAccessor.GetCurrentDb();
        var rows = db.Queryable<ProjectManagementSyncHistoryEntity>()
            .Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.ActorUserId == User() && !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Status)) rows = rows.Where(item => item.Status == query.Status.Trim());
        var total = new RefAsync<int>();
        var items = await rows.OrderBy(item => item.OccurredAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);
        return new ProjectManagementSyncHistoryPage(total.Value, items.Select(Map).ToList());
    }

    public async Task<ProjectManagementSyncHistoryDetail> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await FindOwnedAsync(id, cancellationToken);
        var report = ReadReport(entity.ReportJson);
        return new ProjectManagementSyncHistoryDetail(Map(entity), entity.Strategy, report.Warnings, report.Conflicts);
    }

    public async Task<(string FileName, byte[] Content)> DownloadSafeReportAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await FindOwnedAsync(id, cancellationToken);
        var report = ReadReport(entity.ReportJson);
        var builder = new StringBuilder();
        builder.AppendLine("PackageId,OperationType,Status,SourceTenantId,SourceAppCode,SourceDeviceId,TargetTenantId,TargetAppCode,ActorUserId,OccurredAt,Inserted,Updated,Deleted,Skipped,Conflicts,Failed,AttachmentsImported,TraceId,ErrorMessage");
        builder.AppendLine(string.Join(',', [
            Escape(entity.PackageId), Escape(entity.OperationType), Escape(entity.Status), Escape(entity.SourceTenantId), Escape(entity.SourceAppCode), Escape(entity.SourceDeviceId),
            Escape(entity.TargetTenantId), Escape(entity.TargetAppCode), Escape(entity.ActorUserId), Escape(entity.OccurredAt.ToString("O")), entity.Inserted, entity.Updated, entity.Deleted,
            entity.Skipped, entity.ConflictCount, entity.Failed, entity.AttachmentsImported, Escape(entity.TraceId), Escape(entity.ErrorMessage)]));
        builder.AppendLine();
        builder.AppendLine("AggregateType,AggregateId,ProjectId,Field,LocalVersionNo,RemoteVersionNo,RecommendedStrategy");
        foreach (var conflict in report.Conflicts)
            builder.AppendLine(string.Join(',', [Escape(conflict.AggregateType), Escape(conflict.AggregateId), Escape(conflict.ProjectId), Escape(conflict.Field), conflict.LocalVersionNo, conflict.RemoteVersionNo, Escape(conflict.RecommendedStrategy)]));
        return ($"project-management-sync-report-{entity.PackageId}-{entity.Id}.csv", Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private async Task<ProjectManagementSyncHistoryEntity> FindOwnedAsync(string id, CancellationToken cancellationToken)
    {
        var row = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementSyncHistoryEntity>()
            .Where(item => item.Id == id && item.TenantId == Tenant() && item.AppCode == App() && item.ActorUserId == User() && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken);
        return row.FirstOrDefault() ?? throw new ValidationException("同步历史不存在或无权访问");
    }

    private static ProjectManagementSyncHistoryItem Map(ProjectManagementSyncHistoryEntity item) => new(item.Id, item.OperationType, item.PackageId, item.SourceTenantId, item.SourceAppCode, item.SourceDeviceId, item.TargetTenantId, item.TargetAppCode, item.ActorUserId, item.Status, item.Inserted, item.Updated, item.Deleted, item.Skipped, item.ConflictCount, item.Failed, item.AttachmentsImported, item.TraceId, item.ErrorMessage, item.RetryOfHistoryId, item.OccurredAt);
    private static SyncHistoryReport ReadReport(string json) => JsonSerializer.Deserialize<SyncHistoryReport>(json, JsonOptions) ?? new([], []);
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Escape(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
    private sealed record SyncHistoryReport(IReadOnlyList<string> Warnings, IReadOnlyList<ProjectManagementSyncConflict> Conflicts);
}

using AsterERP.Api.Application.System.Notifications;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.Announcements;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Infrastructure.UnitOfWork;
using AsterERP.Api.Modules.System.Announcements;
using SqlSugar;

namespace AsterERP.Api.Application.System.Announcements;

public sealed class AnnouncementService(
    IRepository<SystemAnnouncementEntity> repository,
    IUnitOfWork unitOfWork,
    INotificationService notificationService,
    ICurrentUser currentUser)
    : IAnnouncementService
{
    private const string DraftStatus = "Draft";
    private const string PublishedStatus = "Published";
    private const string WithdrawnStatus = "Withdrawn";
    private const string RevokedStatus = "Revoked";
    private const string ExpiredStatus = "Expired";
    private const string PublishedEventName = "SystemAnnouncementPublished";
    private const int MaxPageSize = 100;
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemAnnouncementEntity>, OrderByType, ISugarQueryable<SystemAnnouncementEntity>>> Sorters =
        new Dictionary<string, Func<ISugarQueryable<SystemAnnouncementEntity>, OrderByType, ISugarQueryable<SystemAnnouncementEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["announcementType"] = (query, order) => query.OrderBy(item => item.AnnouncementType, order),
            ["createdTime"] = (query, order) => query.OrderBy(item => item.CreatedTime, order),
            ["expiresAt"] = (query, order) => query.OrderBy(item => item.ExpiresAt, order),
            ["isPinned"] = (query, order) => query.OrderBy(item => item.IsTop, order),
            ["isTop"] = (query, order) => query.OrderBy(item => item.IsTop, order),
            ["priority"] = (query, order) => query.OrderBy(item => item.Priority, order),
            ["publishedAt"] = (query, order) => query.OrderBy(item => item.PublishedAt, order),
            ["scope"] = (query, order) => query.OrderBy(item => item.Scope, order),
            ["status"] = (query, order) => query.OrderBy(item => item.Status, order),
            ["title"] = (query, order) => query.OrderBy(item => item.Title, order),
            ["updatedTime"] = (query, order) => query.OrderBy(item => item.UpdatedTime, order)
        };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemAnnouncementEntity>, GridFilter, ISugarQueryable<SystemAnnouncementEntity>>> Filterers =
        new Dictionary<string, Func<ISugarQueryable<SystemAnnouncementEntity>, GridFilter, ISugarQueryable<SystemAnnouncementEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["announcementType"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.AnnouncementType),
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.CreatedTime),
            ["isPinned"] = (query, filter) => GridFilterApplier.ApplyBoolean(query, filter, item => item.IsTop),
            ["isTop"] = (query, filter) => GridFilterApplier.ApplyBoolean(query, filter, item => item.IsTop),
            ["priority"] = (query, filter) => GridFilterApplier.ApplyInt32(query, filter, item => item.Priority),
            ["scope"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Scope),
            ["status"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Status),
            ["title"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Title),
            ["updatedTime"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, item => item.UpdatedTime)
        };

    public async Task<GridPageResult<AnnouncementListItemResponse>> GetPageAsync(
        GridQuery gridQuery,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTime.UtcNow;
        var keyword = gridQuery.Keyword?.Trim();
        var status = NormalizeStatus(gridQuery.Status);
        var pageIndex = Math.Max(gridQuery.PageIndex, 1);
        var pageSize = Math.Clamp(gridQuery.PageSize, 1, MaxPageSize);

        var query = repository.Query()
            .WhereIF(!string.IsNullOrWhiteSpace(keyword), item =>
                item.Title.Contains(keyword!) || item.Content.Contains(keyword!));

        query = GridSortApplier.Apply(
            GridFilterApplier.Apply(ApplyStatusFilter(query, status, now), gridQuery.Filters, Filterers),
            gridQuery.Sorts,
            Sorters,
            ApplyDefaultSort);

        var totalCount = new RefAsync<int>();
        var items = await query.ToPageListAsync(pageIndex, pageSize, totalCount);
        cancellationToken.ThrowIfCancellationRequested();

        return new GridPageResult<AnnouncementListItemResponse>
        {
            Total = totalCount.Value,
            Items = items.Select(item => MapToListItem(item, now)).ToList()
        };
    }

    public async Task<AnnouncementListItemResponse> CreateAsync(
        AnnouncementUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUpsertRequest(request);

        var entity = new SystemAnnouncementEntity
        {
            Status = DraftStatus
        };
        ApplyToEntity(entity, request);

        await repository.InsertAsync(entity, cancellationToken);
        return MapToListItem(entity, DateTime.UtcNow);
    }

    public async Task<AnnouncementListItemResponse> UpdateAsync(
        string id,
        AnnouncementUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUpsertRequest(request);

        var entity = await RequireEntityAsync(id, cancellationToken);
        ApplyToEntity(entity, request);
        await repository.UpdateAsync(entity, cancellationToken);

        return MapToListItem(entity, DateTime.UtcNow);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await RequireEntityAsync(id, cancellationToken);
        await repository.DeleteAsync(id, cancellationToken);
    }

    public async Task<AnnouncementListItemResponse> PublishAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var entity = await RequireEntityAsync(id, cancellationToken);
        var now = DateTime.UtcNow;
        EnsureNotExpiredForPublish(entity, now);

        if (IsEffectivelyPublished(entity, now))
        {
            return MapToListItem(entity, now);
        }

        await unitOfWork.ExecuteAsync(async () =>
        {
            entity.Status = PublishedStatus;
            entity.PublishedAt = now;
            entity.PublishedBy = currentUser.GetAsterErpUserId();
            entity.WithdrawnAt = null;
            await repository.UpdateAsync(entity, cancellationToken);
        }, cancellationToken);

        await notificationService.BroadcastAsync(
            PublishedEventName,
            entity.Title,
            "system",
            cancellationToken);

        return MapToListItem(entity, now);
    }

    public async Task<AnnouncementListItemResponse> WithdrawAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var entity = await RequireEntityAsync(id, cancellationToken);
        var now = DateTime.UtcNow;

        if (entity.Status == WithdrawnStatus)
        {
            return MapToListItem(entity, now);
        }

        entity.Status = WithdrawnStatus;
        entity.IsTop = false;
        entity.WithdrawnAt = now;

        await repository.UpdateAsync(entity, cancellationToken);
        return MapToListItem(entity, now);
    }

    public async Task<AnnouncementListItemResponse> SetTopAsync(
        string id,
        AnnouncementTopRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await RequireEntityAsync(id, cancellationToken);
        var now = DateTime.UtcNow;

        if (request.IsTop && !IsEffectivelyPublished(entity, now))
        {
            throw new ValidationException("仅已发布且未过期的公告可以置顶");
        }

        entity.IsTop = request.IsTop;
        await repository.UpdateAsync(entity, cancellationToken);

        return MapToListItem(entity, now);
    }

    private static ISugarQueryable<SystemAnnouncementEntity> ApplyStatusFilter(
        ISugarQueryable<SystemAnnouncementEntity> query,
        string? status,
        DateTime now)
    {
        return status switch
        {
            DraftStatus => query.Where(item => item.Status == DraftStatus),
            PublishedStatus => query.Where(item =>
                item.Status == PublishedStatus && (item.ExpiresAt == null || item.ExpiresAt > now)),
            WithdrawnStatus => query.Where(item => item.Status == WithdrawnStatus || item.Status == RevokedStatus),
            ExpiredStatus => query.Where(item =>
                item.Status == PublishedStatus && item.ExpiresAt != null && item.ExpiresAt <= now),
            _ => query
        };
    }

    private static void ApplyToEntity(SystemAnnouncementEntity entity, AnnouncementUpsertRequest request)
    {
        entity.Title = request.Title.Trim();
        entity.Content = request.Content.Trim();
        entity.AnnouncementType = string.IsNullOrWhiteSpace(request.AnnouncementType)
            ? "General"
            : request.AnnouncementType.Trim();
        entity.Scope = string.IsNullOrWhiteSpace(request.Scope)
            ? "System"
            : request.Scope.Trim();
        entity.Priority = Math.Max(request.Priority, 0);
        entity.Remark = request.Remark?.Trim();
        entity.ExpiresAt = NormalizeUtc(request.ExpiresAt);

        if (!request.IsPinned)
        {
            entity.IsTop = false;
            return;
        }

        if (entity.Status != PublishedStatus)
        {
            throw new ValidationException("仅已发布公告可以置顶");
        }

        entity.IsTop = true;
    }

    private static AnnouncementListItemResponse MapToListItem(SystemAnnouncementEntity entity, DateTime now) =>
        new(
            entity.Id,
            entity.Title,
            entity.Content,
            entity.AnnouncementType,
            entity.Scope,
            entity.Priority,
            entity.Status,
            GetEffectiveStatus(entity, now),
            entity.IsTop,
            entity.ExpiresAt,
            entity.PublishedAt,
            entity.PublishedBy,
            entity.WithdrawnAt,
            entity.CreatedTime,
            entity.UpdatedTime,
            entity.Remark);

    private static string GetEffectiveStatus(SystemAnnouncementEntity entity, DateTime now)
    {
        return entity.Status == PublishedStatus &&
               entity.ExpiresAt.HasValue &&
               entity.ExpiresAt.Value <= now
            ? ExpiredStatus
            : entity.Status == RevokedStatus
                ? WithdrawnStatus
                : entity.Status;
    }

    private static bool IsEffectivelyPublished(SystemAnnouncementEntity entity, DateTime now) =>
        GetEffectiveStatus(entity, now) == PublishedStatus;

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return status.Trim() switch
        {
            DraftStatus => DraftStatus,
            PublishedStatus => PublishedStatus,
            WithdrawnStatus => WithdrawnStatus,
            RevokedStatus => WithdrawnStatus,
            ExpiredStatus => ExpiredStatus,
            _ => null
        };
    }

    private static ISugarQueryable<SystemAnnouncementEntity> ApplyDefaultSort(ISugarQueryable<SystemAnnouncementEntity> query) =>
        query.OrderBy(item => item.IsTop, OrderByType.Desc)
            .OrderBy(item => item.PublishedAt, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc);

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            DateTimeKind.Utc => value.Value,
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static void ValidateUpsertRequest(AnnouncementUpsertRequest request)
    {
        var expiresAt = NormalizeUtc(request.ExpiresAt);
        if (expiresAt.HasValue && expiresAt.Value <= DateTime.UtcNow)
        {
            throw new ValidationException("过期时间必须晚于当前时间");
        }
    }

    private static void EnsureNotExpiredForPublish(SystemAnnouncementEntity entity, DateTime now)
    {
        if (entity.ExpiresAt.HasValue && entity.ExpiresAt.Value <= now)
        {
            throw new ValidationException("公告已过期，请先调整过期时间后再发布");
        }
    }

    private async Task<SystemAnnouncementEntity> RequireEntityAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var entity = await repository.FirstOrDefaultAsync(
            item => item.Id == id && !item.IsDeleted,
            cancellationToken);

        return entity ?? throw new NotFoundException("公告不存在", ErrorCodes.AnnouncementNotFound);
    }
}

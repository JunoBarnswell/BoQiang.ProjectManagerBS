using System.Linq.Expressions;
using AsterERP.Api.Application.Common;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.Parameters;
using AsterERP.Api.Domain.System.Parameters;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Infrastructure.UnitOfWork;
using AsterERP.Api.Modules.System.Parameters;
using SqlSugar;

namespace AsterERP.Api.Application.System.Parameters;

/// <summary>
/// Implements system parameter CRUD by supplying entity-specific hooks to
/// <see cref="CrudAppServiceBase{TEntity,TListDto,TUpsertRequest}"/>.
/// </summary>
public sealed class ParameterService(
    IRepository<SystemParameterEntity> repository,
    IUnitOfWork unitOfWork)
    : CrudAppServiceBase<SystemParameterEntity, ParameterListItemResponse, ParameterUpsertRequest>(repository, unitOfWork),
      IParameterService
{
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemParameterEntity>, OrderByType, ISugarQueryable<SystemParameterEntity>>> Sorters =
        new Dictionary<string, Func<ISugarQueryable<SystemParameterEntity>, OrderByType, ISugarQueryable<SystemParameterEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["category"] = (query, order) => query.OrderBy(item => item.Category, order),
            ["createdTime"] = (query, order) => query.OrderBy(item => item.CreatedTime, order),
            ["isEnabled"] = (query, order) => query.OrderBy(item => item.IsEnabled, order),
            ["paramKey"] = (query, order) => query.OrderBy(item => item.ParamKey, order),
            ["paramName"] = (query, order) => query.OrderBy(item => item.ParamName, order),
            ["paramValue"] = (query, order) => query.OrderBy(item => item.ParamValue, order),
            ["updatedTime"] = (query, order) => query.OrderBy(item => item.UpdatedTime, order)
        };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemParameterEntity>, GridFilter, ISugarQueryable<SystemParameterEntity>>> Filterers =
        new Dictionary<string, Func<ISugarQueryable<SystemParameterEntity>, GridFilter, ISugarQueryable<SystemParameterEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["category"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Category),
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.CreatedTime),
            ["isEnabled"] = (query, filter) => GridFilterApplier.ApplyBoolean(query, filter, item => item.IsEnabled),
            ["paramKey"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.ParamKey),
            ["paramName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.ParamName),
            ["paramValue"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.ParamValue),
            ["updatedTime"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, item => item.UpdatedTime)
        };

    public async Task<GridPageResult<ParameterListItemResponse>> GetPageAsync(
        GridQuery gridQuery,
        string? category,
        CancellationToken cancellationToken = default)
    {
        var keyword = gridQuery.Keyword?.Trim();
        var status = gridQuery.Status?.Trim();
        var normalizedCategory = category?.Trim();
        var query = Repository.Query();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(e => e.ParamName.Contains(keyword) || e.ParamKey.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(normalizedCategory))
        {
            query = query.Where(e => e.Category == normalizedCategory);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var isEnabled = ParameterDomainPolicy.ToEnabledStatus(status);
            query = query.Where(e => e.IsEnabled == isEnabled);
        }

        query = GridFilterApplier.Apply(query, gridQuery.Filters, Filterers);

        var pageQuery = gridQuery.ToPageQuery();
        var total = await query.CountAsync(cancellationToken);
        var items = await GridSortApplier
            .Apply(query, gridQuery.Sorts, Sorters, ApplyDefaultSort)
            .Skip(pageQuery.SkipCount)
            .Take(Math.Max(pageQuery.PageSize, 1))
            .ToListAsync(cancellationToken);

        return new GridPageResult<ParameterListItemResponse>
        {
            Total = total,
            Items = items.Select(MapToListItem).ToList()
        };
    }

    public async Task BatchUpdateStatusAsync(
        IReadOnlyList<string> ids,
        string status,
        CancellationToken cancellationToken = default)
    {
        var normalizedIds = NormalizeIds(ids);
        if (normalizedIds.Count == 0)
        {
            return;
        }

        var isEnabled = ParameterDomainPolicy.ToEnabledStatus(status);
        var parameters = await Repository.ListAsync(
            e => normalizedIds.Contains(e.Id) && !e.IsDeleted,
            cancellationToken: cancellationToken);

        if (parameters.Count != normalizedIds.Count)
        {
            throw new NotFoundException(GetNotFoundMessage(), GetNotFoundErrorCode());
        }

        foreach (var parameter in parameters)
        {
            parameter.IsEnabled = isEnabled;
        }

        await Repository.UpdateRangeAsync(parameters, cancellationToken);
    }

    protected override Expression<Func<SystemParameterEntity, bool>>? BuildKeywordPredicate(string? keyword)
    {
        return string.IsNullOrWhiteSpace(keyword)
            ? null
            : e => e.ParamName.Contains(keyword) || e.ParamKey.Contains(keyword);
    }

    protected override ParameterListItemResponse MapToListItem(SystemParameterEntity entity)
    {
        var isSensitive = ParameterSensitivityPolicy.IsSensitiveKey(entity.ParamKey);
        var paramValue = ParameterSensitivityPolicy.MaskValue(entity.ParamKey, entity.ParamValue);

        return new(
            entity.Id,
            entity.ParamName,
            entity.ParamKey,
            paramValue,
            isSensitive,
            entity.Category,
            entity.IsEnabled,
            entity.Remark);
    }

    protected override void ApplyToEntity(SystemParameterEntity entity, ParameterUpsertRequest request)
    {
        var normalizedKey = request.ParamKey.Trim();
        var normalizedValue = request.ParamValue.Trim();
        var keepExistingSensitiveValue = ParameterSensitivityPolicy.ShouldKeepExistingValue(
            normalizedKey,
            normalizedValue,
            entity.ParamValue);

        entity.ParamName = request.ParamName.Trim();
        entity.ParamKey = normalizedKey;
        if (!keepExistingSensitiveValue)
        {
            entity.ParamValue = normalizedValue;
        }

        entity.Category = request.Category.Trim();
        entity.IsEnabled = request.IsEnabled;
        entity.Remark = request.Remark?.Trim();
    }

    protected override async Task ValidateAsync(
        ParameterUpsertRequest request,
        string? existingId,
        CancellationToken cancellationToken)
    {
        ParameterDomainPolicy.EnsureUpsertRequest(request.ParamName, request.ParamKey, request.ParamValue, request.Category);

        var normalizedKey = request.ParamKey.Trim();
        var exists = await Repository.ExistsAsync(
            e => e.ParamKey == normalizedKey && e.Id != (existingId ?? string.Empty) && !e.IsDeleted,
            cancellationToken);

        if (exists)
        {
            throw new ValidationException("参数键名已存在", ErrorCodes.DuplicateParameterKey);
        }
    }

    protected override string GetNotFoundMessage() => "系统参数不存在";
    protected override int GetNotFoundErrorCode() => ErrorCodes.ParameterNotFound;

    private static List<string> NormalizeIds(IReadOnlyList<string> ids)
    {
        return ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ISugarQueryable<SystemParameterEntity> ApplyDefaultSort(ISugarQueryable<SystemParameterEntity> query) =>
        query.OrderBy(item => item.CreatedTime, OrderByType.Desc);
}

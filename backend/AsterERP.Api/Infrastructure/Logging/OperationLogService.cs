using AsterERP.Shared;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.Logs;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Modules.System.Logs;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Logging;

public sealed class OperationLogService(IWorkspaceDatabaseAccessor databaseAccessor) : IOperationLogService
{
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemOperationLogEntity>, OrderByType, ISugarQueryable<SystemOperationLogEntity>>> Sorters =
        new Dictionary<string, Func<ISugarQueryable<SystemOperationLogEntity>, OrderByType, ISugarQueryable<SystemOperationLogEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["clientIp"] = (query, order) => query.OrderBy(item => item.ClientIp, order),
            ["createdTime"] = (query, order) => query.OrderBy(item => item.CreatedTime, order),
            ["durationMs"] = (query, order) => query.OrderBy(item => item.DurationMs, order),
            ["isSuccess"] = (query, order) => query.OrderBy(item => item.IsSuccess, order),
            ["moduleName"] = (query, order) => query.OrderBy(item => item.ModuleName, order),
            ["operationType"] = (query, order) => query.OrderBy(item => item.OperationType, order),
            ["requestMethod"] = (query, order) => query.OrderBy(item => item.RequestMethod, order),
            ["requestPath"] = (query, order) => query.OrderBy(item => item.RequestPath, order),
            ["routeDisplayName"] = (query, order) => query.OrderBy(item => item.RouteDisplayName, order),
            ["statusCode"] = (query, order) => query.OrderBy(item => item.StatusCode, order),
            ["traceId"] = (query, order) => query.OrderBy(item => item.TraceId, order),
            ["userName"] = (query, order) => query.OrderBy(item => item.UserName, order)
        };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemOperationLogEntity>, GridFilter, ISugarQueryable<SystemOperationLogEntity>>> Filterers =
        new Dictionary<string, Func<ISugarQueryable<SystemOperationLogEntity>, GridFilter, ISugarQueryable<SystemOperationLogEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["clientIp"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.ClientIp),
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.CreatedTime),
            ["durationMs"] = (query, filter) => GridFilterApplier.ApplyInt64(query, filter, item => item.DurationMs),
            ["isSuccess"] = (query, filter) => GridFilterApplier.ApplyBoolean(query, filter, item => item.IsSuccess),
            ["moduleName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.ModuleName),
            ["operationType"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.OperationType),
            ["requestMethod"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.RequestMethod),
            ["requestPath"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.RequestPath),
            ["routeDisplayName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.RouteDisplayName),
            ["statusCode"] = (query, filter) => GridFilterApplier.ApplyInt32(query, filter, item => item.StatusCode),
            ["traceId"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.TraceId),
            ["userName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.UserName)
        };

    public async Task<IReadOnlyList<OperationLogResponse>> RecentAsync(int take = 20, CancellationToken cancellationToken = default)
    {
        var boundedTake = Math.Clamp(take, 1, 100);
        var logs = await databaseAccessor.GetCurrentDb().Queryable<SystemOperationLogEntity>()
            .Where(log => !log.IsDeleted)
            .OrderBy(log => log.CreatedTime, OrderByType.Desc)
            .Take(boundedTake)
            .ToListAsync(cancellationToken);
        return logs.Select(MapToListResponse).ToList();
    }

    public async Task<GridPageResult<OperationLogResponse>> GetPageAsync(OperationLogQueryRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = GridFilterApplier.Apply(
            ApplyQueryFilters(databaseAccessor.GetCurrentDb().Queryable<SystemOperationLogEntity>().Where(log => !log.IsDeleted), request),
            request.Filters,
            Filterers);

        var totalCount = new RefAsync<int>();
        var logs = await GridSortApplier
            .Apply(query, request.Sorts, Sorters, ApplyDefaultSort)
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return new GridPageResult<OperationLogResponse>
        {
            Total = totalCount.Value,
            Items = logs.Select(MapToListResponse).ToList()
        };
    }

    public async Task<OperationLogDetailResponse> GetDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        var logs = await databaseAccessor.GetCurrentDb().Queryable<SystemOperationLogEntity>()
            .Where(log => log.Id == id && !log.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken);

        var log = logs.FirstOrDefault()
            ?? throw new NotFoundException("操作日志不存在", ErrorCodes.InternalError);

        return MapToDetailResponse(log);
    }

    private static ISugarQueryable<SystemOperationLogEntity> ApplyQueryFilters(
        ISugarQueryable<SystemOperationLogEntity> query,
        OperationLogQueryRequest request)
    {
        var user = request.User?.Trim();
        var moduleName = request.ModuleName?.Trim();
        var requestPath = request.RequestPath?.Trim();
        var requestMethod = request.RequestMethod?.Trim().ToUpperInvariant();
        var traceId = request.TraceId?.Trim();

        if (request.StartTime.HasValue)
        {
            var startTime = request.StartTime.Value;
            query = query.Where(log => log.CreatedTime >= startTime);
        }

        if (request.EndTime.HasValue)
        {
            var endTime = request.EndTime.Value;
            query = query.Where(log => log.CreatedTime <= endTime);
        }

        if (!string.IsNullOrWhiteSpace(user))
        {
            query = query.Where(log =>
                (log.UserId != null && log.UserId.Contains(user)) ||
                (log.UserName != null && log.UserName.Contains(user)));
        }

        if (!string.IsNullOrWhiteSpace(moduleName))
        {
            query = query.Where(log => log.ModuleName != null && log.ModuleName.Contains(moduleName));
        }

        if (!string.IsNullOrWhiteSpace(requestPath))
        {
            query = query.Where(log => log.RequestPath.Contains(requestPath));
        }

        if (!string.IsNullOrWhiteSpace(requestMethod))
        {
            query = query.Where(log => log.RequestMethod == requestMethod);
        }

        if (request.IsSuccess.HasValue)
        {
            query = query.Where(log => log.IsSuccess == request.IsSuccess.Value);
        }

        if (!string.IsNullOrWhiteSpace(traceId))
        {
            query = query.Where(log => log.TraceId.Contains(traceId));
        }

        return query;
    }

    private static OperationLogResponse MapToListResponse(SystemOperationLogEntity log)
    {
        return new OperationLogResponse(
            log.Id,
            log.TraceId,
            log.CorrelationId,
            log.RequestPath,
            log.RequestMethod,
            log.RouteDisplayName,
            log.ModuleName,
            log.OperationType,
            log.ActionName,
            log.ClientIp,
            log.UserName,
            log.StatusCode,
            log.DurationMs,
            log.IsSuccess,
            log.CreatedTime);
    }

    private static OperationLogDetailResponse MapToDetailResponse(SystemOperationLogEntity log)
    {
        return new OperationLogDetailResponse(
            log.Id,
            log.TraceId,
            log.CorrelationId,
            log.RequestPath,
            log.RequestMethod,
            log.RouteDisplayName,
            log.ModuleName,
            log.OperationType,
            log.ActionName,
            log.RequestQuery,
            log.ClientIp,
            log.UserId,
            log.UserName,
            log.ErrorMessage,
            log.ExceptionSummary,
            log.StatusCode,
            log.DurationMs,
            log.IsSuccess,
            log.CreatedTime);
    }

    private static ISugarQueryable<SystemOperationLogEntity> ApplyDefaultSort(ISugarQueryable<SystemOperationLogEntity> query) =>
        query.OrderBy(log => log.CreatedTime, OrderByType.Desc);
}


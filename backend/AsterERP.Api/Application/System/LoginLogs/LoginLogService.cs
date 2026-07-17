using AsterERP.Shared;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.Logs;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Modules.System.Logs;
using SqlSugar;

namespace AsterERP.Api.Application.System.LoginLogs;

public sealed class LoginLogService(
    IRepository<SystemLoginLogEntity> loginLogRepository,
    IWorkspaceDatabaseAccessor databaseAccessor,
    ILogger<LoginLogService> logger) : ILoginLogService
{
    private const int MaxPageSize = 100;
    private const int MaxTextLength = 512;
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemLoginLogEntity>, OrderByType, ISugarQueryable<SystemLoginLogEntity>>> Sorters =
        new Dictionary<string, Func<ISugarQueryable<SystemLoginLogEntity>, OrderByType, ISugarQueryable<SystemLoginLogEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["clientIp"] = (query, order) => query.OrderBy(item => item.ClientIp, order),
            ["createdTime"] = (query, order) => query.OrderBy(item => item.CreatedTime, order),
            ["failureReason"] = (query, order) => query.OrderBy(item => item.FailureReason, order),
            ["isSuccess"] = (query, order) => query.OrderBy(item => item.IsSuccess, order),
            ["loginResult"] = (query, order) => query.OrderBy(item => item.IsSuccess, order),
            ["loginTime"] = (query, order) => query.OrderBy(item => item.LoginTime, order),
            ["traceId"] = (query, order) => query.OrderBy(item => item.TraceId, order),
            ["userAgent"] = (query, order) => query.OrderBy(item => item.UserAgent, order),
            ["userName"] = (query, order) => query.OrderBy(item => item.UserName, order)
        };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemLoginLogEntity>, GridFilter, ISugarQueryable<SystemLoginLogEntity>>> Filterers =
        new Dictionary<string, Func<ISugarQueryable<SystemLoginLogEntity>, GridFilter, ISugarQueryable<SystemLoginLogEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["clientIp"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.ClientIp),
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.CreatedTime),
            ["failureReason"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.FailureReason),
            ["isSuccess"] = (query, filter) => GridFilterApplier.ApplyBoolean(query, filter, item => item.IsSuccess),
            ["loginTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.LoginTime),
            ["traceId"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.TraceId),
            ["userAgent"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.UserAgent),
            ["userName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.UserName)
        };

    public async Task WriteAsync(LoginLogWriteRequest request, CancellationToken cancellationToken = default)
    {
        var loginResult = ResolveLoginResult(request);
        var loginTime = DateTime.UtcNow;
        var entity = new SystemLoginLogEntity
        {
            TraceId = NormalizeTraceId(request.TraceId),
            UserName = NormalizeUserName(request.UserName),
            UserId = NormalizeOptional(request.UserId),
            LoginTime = loginTime,
            IsSuccess = request.IsSuccess,
            LoginResult = loginResult,
            FailureReason = NormalizeOptional(request.FailureReason),
            ClientIp = NormalizeOptional(request.ClientIp, 64),
            UserAgent = NormalizeOptional(request.UserAgent, MaxTextLength),
            CreatedTime = loginTime
        };

        try
        {
            await loginLogRepository.InsertAsync(entity, cancellationToken);
            logger.LogInformation(
                "Login logged: {UserName} -> {LoginResult} ({TraceId})",
                entity.UserName,
                loginResult,
                entity.TraceId);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to write login log for {UserName} ({TraceId})",
                entity.UserName,
                entity.TraceId);
        }
    }

    public async Task<GridPageResult<LoginLogListItemResponse>> GetPageAsync(
        LoginLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var keyword = query.Keyword?.Trim();
        var loginResult = NormalizeLoginResultFilter(query.LoginResult);
        ValidateTimeRange(query.StartTime, query.EndTime);

        var logQuery = databaseAccessor.GetCurrentDb().Queryable<SystemLoginLogEntity>().Where(log => !log.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            logQuery = logQuery.Where(log =>
                log.UserName.Contains(keyword) ||
                (log.ClientIp != null && log.ClientIp.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(loginResult))
        {
            logQuery = ApplyLoginResultFilter(logQuery, loginResult);
        }

        if (query.StartTime.HasValue)
        {
            logQuery = logQuery.Where(log => log.LoginTime >= query.StartTime.Value);
        }

        if (query.EndTime.HasValue)
        {
            logQuery = logQuery.Where(log => log.LoginTime <= query.EndTime.Value);
        }

        logQuery = GridFilterApplier.Apply(logQuery, query.Filters, Filterers);

        var totalCount = new RefAsync<int>();
        var items = await GridSortApplier
            .Apply(logQuery, query.Sorts, Sorters, ApplyDefaultSort)
            .ToPageListAsync(pageIndex, pageSize, totalCount);

        return new GridPageResult<LoginLogListItemResponse>
        {
            Total = totalCount.Value,
            Items = items.Select(MapToListItem).ToList()
        };
    }

    private static LoginLogListItemResponse MapToListItem(SystemLoginLogEntity entity)
    {
        return new LoginLogListItemResponse(
            entity.Id,
            entity.TraceId,
            entity.UserName,
            entity.UserId,
            null,
            ResolveLoginResult(entity.IsSuccess, entity.FailureReason),
            entity.IsSuccess,
            entity.FailureReason,
            entity.ClientIp,
            entity.UserAgent,
            entity.LoginTime);
    }

    private static string NormalizeUserName(string value)
    {
        var normalized = value.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : Truncate(normalized, MaxTextLength);
    }

    private static string NormalizeTraceId(string value)
    {
        var normalized = value.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : Truncate(normalized, MaxTextLength);
    }

    private static string? NormalizeOptional(string? value, int maxLength = MaxTextLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Truncate(value.Trim(), maxLength);
    }

    private static string? NormalizeLoginResultFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (!LoginLogResults.IsKnown(normalized))
        {
            throw new ValidationException("登录结果筛选值无效", ErrorCodes.ParameterInvalid);
        }

        return LoginLogResults.Normalize(normalized);
    }

    private static void ValidateTimeRange(DateTime? startTime, DateTime? endTime)
    {
        if (startTime.HasValue && endTime.HasValue && startTime.Value > endTime.Value)
        {
            throw new ValidationException("开始时间不能晚于结束时间", ErrorCodes.ParameterInvalid);
        }
    }

    private static string ResolveLoginResult(LoginLogWriteRequest request)
    {
        return ResolveLoginResult(request.IsSuccess, request.FailureReason);
    }

    private static string ResolveLoginResult(bool isSuccess, string? failureReason)
    {
        if (isSuccess)
        {
            return LoginLogResults.Success;
        }

        return failureReason?.Trim() switch
        {
            "账号不存在" => LoginLogResults.AccountNotFound,
            "账号错误" => LoginLogResults.AccountNotFound,
            "账号已停用" => LoginLogResults.AccountDisabled,
            "密码错误" => LoginLogResults.PasswordError,
            _ => LoginLogResults.PasswordError
        };
    }

    private static ISugarQueryable<SystemLoginLogEntity> ApplyLoginResultFilter(
        ISugarQueryable<SystemLoginLogEntity> query,
        string loginResult)
    {
        return loginResult switch
        {
            LoginLogResults.Success => query.Where(log => log.IsSuccess),
            LoginLogResults.AccountNotFound => query.Where(log => !log.IsSuccess && log.FailureReason == "账号不存在"),
            LoginLogResults.AccountDisabled => query.Where(log => !log.IsSuccess && log.FailureReason == "账号已停用"),
            LoginLogResults.PasswordError => query.Where(log => !log.IsSuccess && log.FailureReason == "密码错误"),
            _ => query
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static ISugarQueryable<SystemLoginLogEntity> ApplyDefaultSort(ISugarQueryable<SystemLoginLogEntity> query) =>
        query.OrderBy(log => log.LoginTime, OrderByType.Desc);
}


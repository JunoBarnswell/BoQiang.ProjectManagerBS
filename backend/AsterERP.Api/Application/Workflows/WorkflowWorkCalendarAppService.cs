using AsterERP.Api.Modules.Workflows;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Timing;

namespace AsterERP.Api.Application.Workflows;

public sealed class WorkflowWorkCalendarAppService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IClock clock) : IWorkflowWorkCalendarAppService
{
    private static readonly HashSet<string> AllowedDayTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Workday",
        "Holiday",
        "AdjustedWorkday"
    };

    public async Task<GridPageResult<WorkflowWorkCalendarResponse>> GetPageAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenant(query.TenantId);
        var appCode = ResolveApp(query.AppCode);
        var pageIndex = Math.Max(query.PageIndex, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var total = new RefAsync<int>();
        var entities = await databaseAccessor.GetCurrentDb().Queryable<WorkflowWorkCalendarEntity>()
            .Where(item => !item.IsDeleted && item.TenantId == tenantId && item.AppCode == appCode)
            .WhereIF(!string.IsNullOrWhiteSpace(query.Keyword), item =>
                item.CalendarName.Contains(query.Keyword!) ||
                item.DayType.Contains(query.Keyword!))
            .WhereIF(!string.IsNullOrWhiteSpace(query.Status), item => item.DayType == query.Status)
            .OrderBy(item => item.CalendarDate, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);

        return new GridPageResult<WorkflowWorkCalendarResponse>
        {
            Total = total.Value,
            Items = entities.Select(Map).ToList()
        };
    }

    public async Task<WorkflowWorkCalendarResponse> SaveAsync(
        WorkflowWorkCalendarUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenant(request.TenantId);
        var appCode = ResolveApp(request.AppCode);
        var calendarDate = request.CalendarDate.Date;
        if (calendarDate.Year < 2000 || calendarDate.Year > 2100)
        {
            throw new ValidationException("工作日历日期必须位于 2000-2100 年之间", ErrorCodes.ParameterInvalid);
        }

        var dayType = NormalizeRequired(request.DayType, "日期类型不能为空");
        if (!AllowedDayTypes.Contains(dayType))
        {
            throw new ValidationException("日期类型只能为 Workday、Holiday 或 AdjustedWorkday", ErrorCodes.ParameterInvalid);
        }

        var duplicate = await databaseAccessor.GetCurrentDb().Queryable<WorkflowWorkCalendarEntity>()
            .AnyAsync(item =>
                    !item.IsDeleted &&
                    item.TenantId == tenantId &&
                    item.AppCode == appCode &&
                    item.CalendarDate == calendarDate &&
                    item.Id != (request.Id ?? string.Empty),
                cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("该日期已配置工作日历", ErrorCodes.WorkflowCalendarDuplicate);
        }

        WorkflowWorkCalendarEntity entity;
        if (string.IsNullOrWhiteSpace(request.Id))
        {
            entity = new WorkflowWorkCalendarEntity
            {
                TenantId = tenantId,
                AppCode = appCode,
                CalendarDate = calendarDate,
                DayType = dayType,
                IsWorkingDay = request.IsWorkingDay ?? !string.Equals(dayType, "Holiday", StringComparison.OrdinalIgnoreCase),
                CalendarName = NormalizeRequired(request.CalendarName, "日历名称不能为空"),
                Remark = request.Remark
            };
            await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            entity = await GetRequiredAsync(request.Id, cancellationToken);
            entity.CalendarDate = calendarDate;
            entity.DayType = dayType;
            entity.IsWorkingDay = request.IsWorkingDay ?? !string.Equals(dayType, "Holiday", StringComparison.OrdinalIgnoreCase);
            entity.CalendarName = NormalizeRequired(request.CalendarName, "日历名称不能为空");
            entity.Remark = request.Remark;
            entity.UpdatedBy = currentUser.GetAsterErpUserId();
            entity.UpdatedTime = clock.Now;
            await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }

        return Map(entity);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedBy = currentUser.GetAsterErpUserId();
        entity.DeletedTime = clock.Now;
        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<WorkflowWorkCalendarEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        return await databaseAccessor.GetCurrentDb().Queryable<WorkflowWorkCalendarEntity>()
            .FirstAsync(item => item.Id == id && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("工作日历不存在", ErrorCodes.WorkflowCalendarNotFound);
    }

    private string ResolveTenant(string? tenantId) =>
        NormalizeRequired(tenantId, currentUser.GetAsterErpTenantId(), "租户不能为空");

    private string ResolveApp(string? appCode) =>
        NormalizeRequired(appCode, currentUser.GetAsterErpAppCode(), "应用不能为空").ToUpperInvariant();

    private static string NormalizeRequired(string? value, string message) =>
        NormalizeRequired(value, null, message);

    private static string NormalizeRequired(string? value, string? fallback, string message)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ValidationException(message, ErrorCodes.ParameterInvalid);
        }

        return normalized;
    }

    private static WorkflowWorkCalendarResponse Map(WorkflowWorkCalendarEntity entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.AppCode,
            entity.CalendarDate,
            entity.DayType,
            entity.IsWorkingDay,
            entity.CalendarName,
            entity.Remark,
            entity.CreatedTime,
            entity.UpdatedTime);
}


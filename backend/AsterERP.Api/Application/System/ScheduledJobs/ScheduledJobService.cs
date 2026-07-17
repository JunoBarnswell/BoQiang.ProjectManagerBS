using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.ScheduledJobs;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Domain.System.ScheduledJobs;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Infrastructure.UnitOfWork;
using AsterERP.Api.Modules.System.ScheduledJobs;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace AsterERP.Api.Application.System.ScheduledJobs;

public sealed class ScheduledJobService(
    IRepository<SystemScheduledJobEntity> scheduledJobRepository,
    IRepository<SystemScheduledJobLogEntity> scheduledJobLogRepository,
    IUnitOfWork unitOfWork,
    IScheduledJobScheduler scheduler,
    ScheduledJobDomainPolicy domainPolicy,
    ScheduledJobTypeCatalog typeCatalog,
    IOptions<SchedulerOptions> schedulerOptions)
    : IScheduledJobService
{
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemScheduledJobEntity>, OrderByType, ISugarQueryable<SystemScheduledJobEntity>>> JobSorters =
        new Dictionary<string, Func<ISugarQueryable<SystemScheduledJobEntity>, OrderByType, ISugarQueryable<SystemScheduledJobEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = (query, order) => query.OrderBy(item => item.JobCode, order),
            ["createdTime"] = (query, order) => query.OrderBy(item => item.CreatedTime, order),
            ["jobCode"] = (query, order) => query.OrderBy(item => item.JobCode, order),
            ["jobName"] = (query, order) => query.OrderBy(item => item.JobName, order),
            ["jobType"] = (query, order) => query.OrderBy(item => item.JobType, order),
            ["lastResult"] = (query, order) => query.OrderBy(item => item.LastResult, order),
            ["lastRunAt"] = (query, order) => query.OrderBy(item => item.LastRunAt, order),
            ["name"] = (query, order) => query.OrderBy(item => item.JobName, order),
            ["nextRunAt"] = (query, order) => query.OrderBy(item => item.NextRunAt, order),
            ["scheduleSyncStatus"] = (query, order) => query.OrderBy(item => item.ScheduleSyncStatus, order),
            ["status"] = (query, order) => query.OrderBy(item => item.Status, order),
            ["updatedTime"] = (query, order) => query.OrderBy(item => item.UpdatedTime, order)
        };
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemScheduledJobLogEntity>, OrderByType, ISugarQueryable<SystemScheduledJobLogEntity>>> LogSorters =
        new Dictionary<string, Func<ISugarQueryable<SystemScheduledJobLogEntity>, OrderByType, ISugarQueryable<SystemScheduledJobLogEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdTime"] = (query, order) => query.OrderBy(item => item.CreatedTime, order),
            ["durationMs"] = (query, order) => query.OrderBy(item => item.DurationMs, order),
            ["endTime"] = (query, order) => query.OrderBy(item => item.FinishedAt, order),
            ["errorMessage"] = (query, order) => query.OrderBy(item => item.ErrorMessage, order),
            ["finishedAt"] = (query, order) => query.OrderBy(item => item.FinishedAt, order),
            ["jobId"] = (query, order) => query.OrderBy(item => item.HangfireJobId, order),
            ["hangfireJobId"] = (query, order) => query.OrderBy(item => item.HangfireJobId, order),
            ["outputSummary"] = (query, order) => query.OrderBy(item => item.OutputSummary, order),
            ["result"] = (query, order) => query.OrderBy(item => item.Result, order),
            ["startTime"] = (query, order) => query.OrderBy(item => item.StartedAt, order),
            ["startedAt"] = (query, order) => query.OrderBy(item => item.StartedAt, order),
            ["traceId"] = (query, order) => query.OrderBy(item => item.TraceId, order),
            ["triggerType"] = (query, order) => query.OrderBy(item => item.TriggerType, order)
        };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemScheduledJobEntity>, GridFilter, ISugarQueryable<SystemScheduledJobEntity>>> JobFilterers =
        new Dictionary<string, Func<ISugarQueryable<SystemScheduledJobEntity>, GridFilter, ISugarQueryable<SystemScheduledJobEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.CreatedTime),
            ["jobCode"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.JobCode),
            ["jobName"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.JobName),
            ["jobType"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.JobType),
            ["lastResult"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.LastResult),
            ["lastRunAt"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, item => item.LastRunAt),
            ["nextRunAt"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, item => item.NextRunAt),
            ["scheduleSyncStatus"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.ScheduleSyncStatus),
            ["status"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Status),
            ["updatedTime"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, item => item.UpdatedTime)
        };

    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<SystemScheduledJobLogEntity>, GridFilter, ISugarQueryable<SystemScheduledJobLogEntity>>> LogFilterers =
        new Dictionary<string, Func<ISugarQueryable<SystemScheduledJobLogEntity>, GridFilter, ISugarQueryable<SystemScheduledJobLogEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdTime"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.CreatedTime),
            ["durationMs"] = (query, filter) => GridFilterApplier.ApplyInt64(query, filter, item => item.DurationMs),
            ["errorMessage"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.ErrorMessage),
            ["finishedAt"] = (query, filter) => GridFilterApplier.ApplyNullableDateTime(query, filter, item => item.FinishedAt),
            ["hangfireJobId"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.HangfireJobId),
            ["outputSummary"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.OutputSummary),
            ["result"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.Result),
            ["startedAt"] = (query, filter) => GridFilterApplier.ApplyDateTime(query, filter, item => item.StartedAt),
            ["traceId"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.TraceId),
            ["triggerType"] = (query, filter) => GridFilterApplier.ApplyString(query, filter, item => item.TriggerType)
        };

    public async Task<GridPageResult<ScheduledJobListItemResponse>> GetPageAsync(
        GridQuery gridQuery,
        string? jobType,
        string? result,
        CancellationToken cancellationToken = default)
    {
        var pageQuery = gridQuery.ToPageQuery();
        var keyword = gridQuery.Keyword?.Trim();
        var status = gridQuery.Status?.Trim();
        var normalizedJobType = jobType?.Trim();
        var normalizedResult = result?.Trim();

        var query = scheduledJobRepository.Query();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(job => job.JobName.Contains(keyword) || job.JobCode.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(job => job.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(normalizedJobType))
        {
            query = query.Where(job => job.JobType == normalizedJobType);
        }

        if (!string.IsNullOrWhiteSpace(normalizedResult))
        {
            query = query.Where(job => job.LastResult == normalizedResult);
        }

        query = GridFilterApplier.Apply(query, gridQuery.Filters, JobFilterers);

        var total = await query.CountAsync(cancellationToken);
        var items = await GridSortApplier
            .Apply(query, gridQuery.Sorts, JobSorters, ApplyDefaultJobSort)
            .Skip(pageQuery.SkipCount)
            .Take(pageQuery.PageSize)
            .ToListAsync(cancellationToken);

        return new GridPageResult<ScheduledJobListItemResponse>
        {
            Total = total,
            Items = items.Select(MapToListItem).ToList()
        };
    }

    public async Task<ScheduledJobSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await scheduledJobRepository.ListAsync(cancellationToken: cancellationToken);
        return new ScheduledJobSummaryResponse(
            jobs.Count,
            jobs.Count(job => job.Status == ScheduledJobConstants.StatusEnabled),
            jobs.Count(job => job.Status == ScheduledJobConstants.StatusPaused),
            jobs.Count(job => job.LastResult == ScheduledJobConstants.ResultSuccess),
            jobs.Count(job => job.LastResult == ScheduledJobConstants.ResultFailed));
    }

    public Task<ScheduledJobTypesResponse> GetTypesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ScheduledJobTypesResponse(
            typeCatalog.GetPresetJobs(),
            [ScheduledJobConstants.JobTypePreset, ScheduledJobConstants.JobTypeHttpCallback],
            ["GET", "POST"],
            [
                ScheduledJobConstants.ScheduleEveryMinutes,
                ScheduledJobConstants.ScheduleEveryHours,
                ScheduledJobConstants.ScheduleDaily,
                ScheduledJobConstants.ScheduleWeekly,
                ScheduledJobConstants.ScheduleMonthly
            ]));
    }

    public async Task<ScheduledJobDetailResponse> GetDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        return MapToDetail(entity);
    }

    public async Task<ScheduledJobListItemResponse> CreateAsync(ScheduledJobUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var schedule = domainPolicy.EnsureUpsertRequest(request, schedulerOptions.Value);
        await EnsureUniqueCodeAsync(request.Code.Trim(), null, cancellationToken);

        var entity = new SystemScheduledJobEntity();
        ApplyToEntity(entity, request, schedule);

        await unitOfWork.ExecuteAsync(
            async () => await scheduledJobRepository.InsertAsync(entity, cancellationToken),
            cancellationToken);

        await SynchronizeEntityAsync(entity, cancellationToken);
        return MapToListItem(entity);
    }

    public async Task<ScheduledJobListItemResponse> UpdateAsync(string id, ScheduledJobUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        var schedule = domainPolicy.EnsureUpsertRequest(request, schedulerOptions.Value);
        await EnsureUniqueCodeAsync(request.Code.Trim(), id, cancellationToken);

        ApplyToEntity(entity, request, schedule);
        await unitOfWork.ExecuteAsync(
            async () => await scheduledJobRepository.UpdateAsync(entity, cancellationToken),
            cancellationToken);

        await SynchronizeEntityAsync(entity, cancellationToken);
        return MapToListItem(entity);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        await scheduledJobRepository.DeleteAsync(id, cancellationToken);
        await scheduler.RemoveAsync(entity, cancellationToken);
    }

    public async Task PauseAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        entity.Status = ScheduledJobConstants.StatusPaused;
        await scheduledJobRepository.UpdateAsync(entity, cancellationToken);
        await SynchronizeEntityAsync(entity, cancellationToken);
    }

    public async Task ResumeAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        entity.Status = ScheduledJobConstants.StatusEnabled;
        var schedule = ScheduledJobDomainPolicy.Deserialize<ScheduleConfigDto>(entity.ScheduleConfigJson)
            ?? throw new ValidationException("任务周期配置缺失", ErrorCodes.ScheduledJobScheduleInvalid);
        var buildResult = domainPolicy.EnsureUpsertRequest(BuildRequest(entity, schedule), schedulerOptions.Value);
        entity.CronExpression = buildResult.CronExpression;
        entity.FriendlySchedule = buildResult.FriendlySchedule;
        entity.NextRunAt = buildResult.NextRunAt;
        await scheduledJobRepository.UpdateAsync(entity, cancellationToken);
        await SynchronizeEntityAsync(entity, cancellationToken);
    }

    public async Task<string> TriggerAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await GetRequiredAsync(id, cancellationToken);
        var hangfireJobId = await scheduler.EnqueueAsync(entity, cancellationToken);
        await scheduledJobLogRepository.InsertAsync(new SystemScheduledJobLogEntity
        {
            ScheduledJobId = entity.Id,
            HangfireJobId = hangfireJobId,
            TriggerType = ScheduledJobConstants.TriggerManual,
            Result = ScheduledJobConstants.ResultQueued,
            StartedAt = DateTime.UtcNow,
            TraceId = Guid.NewGuid().ToString("N"),
            OutputSummary = "任务已进入执行队列"
        }, cancellationToken);

        return hangfireJobId;
    }

    public async Task<GridPageResult<ScheduledJobLogResponse>> GetLogsAsync(
        string id,
        GridQuery gridQuery,
        string? result,
        CancellationToken cancellationToken = default)
    {
        _ = await GetRequiredAsync(id, cancellationToken);
        var pageQuery = gridQuery.ToPageQuery();
        var normalizedResult = result?.Trim();
        var query = scheduledJobLogRepository.Query().Where(log => log.ScheduledJobId == id);

        if (!string.IsNullOrWhiteSpace(normalizedResult))
        {
            query = query.Where(log => log.Result == normalizedResult);
        }

        query = GridFilterApplier.Apply(query, gridQuery.Filters, LogFilterers);

        var total = await query.CountAsync(cancellationToken);
        var items = await GridSortApplier
            .Apply(query, gridQuery.Sorts, LogSorters, ApplyDefaultLogSort)
            .Skip(pageQuery.SkipCount)
            .Take(pageQuery.PageSize)
            .ToListAsync(cancellationToken);

        return new GridPageResult<ScheduledJobLogResponse>
        {
            Total = total,
            Items = items.Select(MapLog).ToList()
        };
    }

    public async Task SynchronizeAllAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await scheduledJobRepository.ListAsync(cancellationToken: cancellationToken);
        foreach (var job in jobs)
        {
            await SynchronizeEntityAsync(job, cancellationToken);
        }
    }

    private async Task SynchronizeEntityAsync(SystemScheduledJobEntity entity, CancellationToken cancellationToken)
    {
        try
        {
            if (entity.Status == ScheduledJobConstants.StatusEnabled)
            {
                await scheduler.RegisterOrUpdateAsync(entity, cancellationToken);
                entity.ScheduleSyncStatus = "Synced";
            }
            else
            {
                await scheduler.RemoveAsync(entity, cancellationToken);
                entity.ScheduleSyncStatus = "Paused";
                entity.NextRunAt = null;
            }

            entity.LastSyncError = null;
            await scheduledJobRepository.UpdateAsync(entity, cancellationToken);
        }
        catch (Exception ex)
        {
            entity.ScheduleSyncStatus = "Failed";
            entity.LastSyncError = ex.Message;
            await scheduledJobRepository.UpdateAsync(entity, cancellationToken);
            throw new BusinessException(ErrorCodes.ScheduledJobSyncFailed, "任务调度同步失败");
        }
    }

    private async Task<SystemScheduledJobEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        return await scheduledJobRepository.FirstOrDefaultAsync(job => job.Id == id && !job.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("任务不存在", ErrorCodes.ScheduledJobNotFound);
    }

    private async Task EnsureUniqueCodeAsync(string code, string? currentId, CancellationToken cancellationToken)
    {
        var exists = await scheduledJobRepository.ExistsAsync(
            job => job.JobCode == code && job.Id != (currentId ?? string.Empty) && !job.IsDeleted,
            cancellationToken);
        if (exists)
        {
            throw new ValidationException("任务编码已存在", ErrorCodes.DuplicateScheduledJobCode);
        }
    }

    private static void ApplyToEntity(SystemScheduledJobEntity entity, ScheduledJobUpsertRequest request, ScheduleBuildResult schedule)
    {
        var scheduleConfigJson = ScheduledJobDomainPolicy.Serialize(request.Schedule);
        entity.JobName = request.Name.Trim();
        entity.JobCode = request.Code.Trim();
        entity.JobType = request.JobType.Trim();
        entity.PresetJobCode = string.IsNullOrWhiteSpace(request.PresetJobCode) ? null : request.PresetJobCode.Trim();
        entity.Status = request.Status.Trim();
        entity.ScheduleKind = request.Schedule.Kind.Trim();
        entity.IntervalValue = request.Schedule.IntervalValue;
        entity.TimeOfDay = request.Schedule.TimeOfDay?.Trim();
        entity.WeekDaysJson = request.Schedule.WeekDays is null ? null : ScheduledJobDomainPolicy.Serialize(request.Schedule.WeekDays);
        entity.MonthDaysJson = request.Schedule.MonthDays is null ? null : ScheduledJobDomainPolicy.Serialize(request.Schedule.MonthDays);
        entity.TimeZoneId = schedule.TimeZoneId;
        entity.ScheduleConfigJson = scheduleConfigJson;
        entity.ParameterJson = string.IsNullOrWhiteSpace(request.Parameters) ? null : request.Parameters.Trim();
        entity.HttpCallbackJson = request.HttpCallback is null ? null : ScheduledJobDomainPolicy.Serialize(request.HttpCallback);
        entity.CronExpression = schedule.CronExpression;
        entity.FriendlySchedule = schedule.FriendlySchedule;
        entity.NextRunAt = request.Status == ScheduledJobConstants.StatusEnabled ? schedule.NextRunAt : null;
        entity.Remark = request.Remark?.Trim();
    }

    private static ScheduledJobUpsertRequest BuildRequest(SystemScheduledJobEntity entity, ScheduleConfigDto schedule) =>
        new(
            entity.JobName,
            entity.JobCode,
            entity.JobType,
            entity.Status,
            entity.PresetJobCode,
            schedule,
            entity.ParameterJson,
            ScheduledJobDomainPolicy.Deserialize<HttpCallbackConfigDto>(entity.HttpCallbackJson),
            entity.Remark);

    private static ScheduledJobListItemResponse MapToListItem(SystemScheduledJobEntity entity) =>
        new(
            entity.Id,
            entity.JobName,
            entity.JobCode,
            entity.JobType,
            entity.PresetJobCode,
            entity.Status,
            entity.FriendlySchedule,
            entity.LastResult,
            entity.LastRunAt,
            entity.NextRunAt,
            entity.ScheduleSyncStatus,
            entity.CreatedTime,
            entity.Remark);

    private static ScheduledJobDetailResponse MapToDetail(SystemScheduledJobEntity entity) =>
        new(
            entity.Id,
            entity.JobName,
            entity.JobCode,
            entity.JobType,
            entity.PresetJobCode,
            entity.Status,
            ScheduledJobDomainPolicy.Deserialize<ScheduleConfigDto>(entity.ScheduleConfigJson) ?? new ScheduleConfigDto(entity.ScheduleKind, entity.IntervalValue, entity.TimeOfDay, null, null, entity.TimeZoneId),
            entity.ParameterJson,
            ScheduledJobDomainPolicy.Deserialize<HttpCallbackConfigDto>(entity.HttpCallbackJson),
            entity.FriendlySchedule,
            entity.LastResult,
            entity.LastRunAt,
            entity.NextRunAt,
            entity.ScheduleSyncStatus,
            entity.LastSyncError,
            entity.LastErrorMessage,
            entity.Remark);

    private static ScheduledJobLogResponse MapLog(SystemScheduledJobLogEntity log) =>
        new(
            log.Id,
            log.HangfireJobId,
            log.TriggerType,
            log.Result,
            log.StartedAt,
            log.FinishedAt,
            log.DurationMs,
            log.ErrorMessage,
            log.OutputSummary,
            log.TraceId);

    private static ISugarQueryable<SystemScheduledJobEntity> ApplyDefaultJobSort(ISugarQueryable<SystemScheduledJobEntity> query) =>
        query.OrderBy(job => job.CreatedTime, OrderByType.Desc);

    private static ISugarQueryable<SystemScheduledJobLogEntity> ApplyDefaultLogSort(ISugarQueryable<SystemScheduledJobLogEntity> query) =>
        query.OrderBy(log => log.CreatedTime, OrderByType.Desc);
}

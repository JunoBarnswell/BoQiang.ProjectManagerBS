using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Contracts.System.ScheduledJobs;

namespace AsterERP.Api.Domain.System.ScheduledJobs;

public sealed class ScheduleExpressionBuilder
{
    public ScheduleBuildResult Build(ScheduleConfigDto schedule)
    {
        var kind = schedule.Kind.Trim();
        var timeZoneId = ResolveTimeZoneId(schedule.TimeZone);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        var result = kind switch
        {
            ScheduledJobConstants.ScheduleEveryMinutes => BuildEveryMinutes(schedule, timeZone),
            ScheduledJobConstants.ScheduleEveryHours => BuildEveryHours(schedule, timeZone),
            ScheduledJobConstants.ScheduleDaily => BuildDaily(schedule, timeZone),
            ScheduledJobConstants.ScheduleWeekly => BuildWeekly(schedule, timeZone),
            ScheduledJobConstants.ScheduleMonthly => BuildMonthly(schedule, timeZone),
            _ => throw new ValidationException("不支持的执行周期", ErrorCodes.ScheduledJobScheduleInvalid)
        };

        return result with { TimeZoneId = timeZoneId };
    }

    private static ScheduleBuildResult BuildEveryMinutes(ScheduleConfigDto schedule, TimeZoneInfo timeZone)
    {
        var interval = RequireRange(schedule.IntervalValue, 1, 59, "分钟间隔必须在 1 到 59 之间");
        var cron = interval == 1 ? "* * * * *" : $"*/{interval} * * * *";
        return new ScheduleBuildResult(cron, $"每 {interval} 分钟执行一次", CalculateNextRun(schedule, timeZone), timeZone.Id);
    }

    private static ScheduleBuildResult BuildEveryHours(ScheduleConfigDto schedule, TimeZoneInfo timeZone)
    {
        var interval = RequireRange(schedule.IntervalValue, 1, 23, "小时间隔必须在 1 到 23 之间");
        return new ScheduleBuildResult($"0 */{interval} * * *", $"每 {interval} 小时执行一次", CalculateNextRun(schedule, timeZone), timeZone.Id);
    }

    private static ScheduleBuildResult BuildDaily(ScheduleConfigDto schedule, TimeZoneInfo timeZone)
    {
        var time = RequireTime(schedule.TimeOfDay);
        return new ScheduleBuildResult($"{time.Minutes} {time.Hours} * * *", $"每天 {time:hh\\:mm} 执行", CalculateNextRun(schedule, timeZone), timeZone.Id);
    }

    private static ScheduleBuildResult BuildWeekly(ScheduleConfigDto schedule, TimeZoneInfo timeZone)
    {
        var time = RequireTime(schedule.TimeOfDay);
        var weekDays = NormalizeValues(schedule.WeekDays, 0, 6, "请选择每周执行日期");
        var weekText = string.Join("、", weekDays.Select(ToWeekDayText));
        return new ScheduleBuildResult(
            $"{time.Minutes} {time.Hours} * * {string.Join(",", weekDays)}",
            $"每周{weekText} {time:hh\\:mm} 执行",
            CalculateNextRun(schedule, timeZone),
            timeZone.Id);
    }

    private static ScheduleBuildResult BuildMonthly(ScheduleConfigDto schedule, TimeZoneInfo timeZone)
    {
        var time = RequireTime(schedule.TimeOfDay);
        var monthDays = NormalizeValues(schedule.MonthDays, 1, 31, "请选择每月执行日期");
        return new ScheduleBuildResult(
            $"{time.Minutes} {time.Hours} {string.Join(",", monthDays)} * *",
            $"每月 {string.Join("、", monthDays)} 日 {time:hh\\:mm} 执行",
            CalculateNextRun(schedule, timeZone),
            timeZone.Id);
    }

    private static DateTime? CalculateNextRun(ScheduleConfigDto schedule, TimeZoneInfo timeZone)
    {
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime;

        return schedule.Kind.Trim() switch
        {
            ScheduledJobConstants.ScheduleEveryMinutes => now.AddMinutes(RequireRange(schedule.IntervalValue, 1, 59, "分钟间隔必须在 1 到 59 之间")).ToUniversalTime(),
            ScheduledJobConstants.ScheduleEveryHours => now.AddHours(RequireRange(schedule.IntervalValue, 1, 23, "小时间隔必须在 1 到 23 之间")).ToUniversalTime(),
            ScheduledJobConstants.ScheduleDaily => NextDaily(now, RequireTime(schedule.TimeOfDay)).ToUniversalTime(),
            ScheduledJobConstants.ScheduleWeekly => NextWeekly(now, RequireTime(schedule.TimeOfDay), NormalizeValues(schedule.WeekDays, 0, 6, "请选择每周执行日期")).ToUniversalTime(),
            ScheduledJobConstants.ScheduleMonthly => NextMonthly(now, RequireTime(schedule.TimeOfDay), NormalizeValues(schedule.MonthDays, 1, 31, "请选择每月执行日期")).ToUniversalTime(),
            _ => null
        };
    }

    private static DateTime NextDaily(DateTime now, TimeSpan time)
    {
        var candidate = now.Date.Add(time);
        return candidate > now ? candidate : candidate.AddDays(1);
    }

    private static DateTime NextWeekly(DateTime now, TimeSpan time, IReadOnlyList<int> weekDays)
    {
        for (var offset = 0; offset <= 7; offset++)
        {
            var candidateDate = now.Date.AddDays(offset);
            var cronDay = (int)candidateDate.DayOfWeek;
            var candidate = candidateDate.Add(time);
            if (weekDays.Contains(cronDay) && candidate > now)
            {
                return candidate;
            }
        }

        return now.Date.AddDays(7).Add(time);
    }

    private static DateTime NextMonthly(DateTime now, TimeSpan time, IReadOnlyList<int> monthDays)
    {
        for (var monthOffset = 0; monthOffset <= 2; monthOffset++)
        {
            var cursor = new DateTime(now.Year, now.Month, 1).AddMonths(monthOffset);
            var daysInMonth = DateTime.DaysInMonth(cursor.Year, cursor.Month);
            foreach (var day in monthDays.Where(day => day <= daysInMonth).OrderBy(day => day))
            {
                var candidate = new DateTime(cursor.Year, cursor.Month, day).Add(time);
                if (candidate > now)
                {
                    return candidate;
                }
            }
        }

        return now.Date.AddMonths(1).Add(time);
    }

    private static int RequireRange(int? value, int min, int max, string message)
    {
        if (!value.HasValue || value.Value < min || value.Value > max)
        {
            throw new ValidationException(message, ErrorCodes.ScheduledJobScheduleInvalid);
        }

        return value.Value;
    }

    private static TimeSpan RequireTime(string? value)
    {
        if (!TimeSpan.TryParse(value, out var time) || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1))
        {
            throw new ValidationException("执行时间格式无效", ErrorCodes.ScheduledJobScheduleInvalid);
        }

        return time;
    }

    private static IReadOnlyList<int> NormalizeValues(IReadOnlyList<int>? values, int min, int max, string emptyMessage)
    {
        var normalized = values?
            .Where(value => value >= min && value <= max)
            .Distinct()
            .OrderBy(value => value)
            .ToArray() ?? [];

        if (normalized.Length == 0)
        {
            throw new ValidationException(emptyMessage, ErrorCodes.ScheduledJobScheduleInvalid);
        }

        return normalized;
    }

    private static string ResolveTimeZoneId(string? timeZoneId)
    {
        var candidate = string.IsNullOrWhiteSpace(timeZoneId) ? "China Standard Time" : timeZoneId.Trim();
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(candidate);
            return candidate;
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ValidationException("时区无效", ErrorCodes.ScheduledJobScheduleInvalid);
        }
        catch (InvalidTimeZoneException)
        {
            throw new ValidationException("时区无效", ErrorCodes.ScheduledJobScheduleInvalid);
        }
    }

    private static string ToWeekDayText(int value) =>
        value switch
        {
            0 => "日",
            1 => "一",
            2 => "二",
            3 => "三",
            4 => "四",
            5 => "五",
            6 => "六",
            _ => value.ToString()
        };
}

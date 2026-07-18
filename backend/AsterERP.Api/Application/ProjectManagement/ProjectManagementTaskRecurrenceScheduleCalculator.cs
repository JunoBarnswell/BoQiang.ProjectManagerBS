using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed record ProjectManagementTaskRecurrenceSchedule(DateTime LocalTime, DateTime UtcTime, string RecurrenceKey);

/// <summary>以规则声明的本地挂钟时间计算实例，避免以 UTC 加月导致月底、DST 语义漂移。</summary>
public static class ProjectManagementTaskRecurrenceScheduleCalculator
{
    public static IReadOnlyList<ProjectManagementTaskRecurrenceSchedule> Expand(
        string recurrenceId,
        string frequency,
        int interval,
        IReadOnlyCollection<DayOfWeek> daysOfWeek,
        int? dayOfMonth,
        string? customUnit,
        DateTime startAtLocal,
        DateTime? endsAtLocal,
        string timeZoneId,
        DateTime fromUtc,
        DateTime untilUtc,
        int maximumCount)
    {
        if (string.IsNullOrWhiteSpace(recurrenceId)) throw new ValidationException("重复规则标识不能为空");
        if (interval is < 1 or > 366) throw new ValidationException("重复间隔必须在 1 到 366 之间");
        if (maximumCount is < 1 or > 5000) throw new ValidationException("生成实例上限无效");
        if (untilUtc < fromUtc) return [];
        var zone = ResolveTimeZone(timeZoneId);
        var normalizedStart = AsUnspecified(startAtLocal);
        DateTime? normalizedEnd = endsAtLocal.HasValue ? AsUnspecified(endsAtLocal.Value) : null;
        if (normalizedEnd.HasValue && normalizedEnd < normalizedStart) throw new ValidationException("重复结束时间不能早于开始时间");

        var unit = NormalizeUnit(frequency, customUnit);
        ValidateWeeklyDays(unit, daysOfWeek);
        ValidateMonthDay(unit, dayOfMonth);
        var results = new List<ProjectManagementTaskRecurrenceSchedule>();
        foreach (var local in EnumerateLocalTimes(unit, interval, daysOfWeek, dayOfMonth, normalizedStart, normalizedEnd))
        {
            var utc = ConvertLocalToUtc(local, zone);
            if (utc < fromUtc) continue;
            if (utc > untilUtc) break;
            results.Add(new ProjectManagementTaskRecurrenceSchedule(local, utc, BuildRecurrenceKey(local)));
            if (results.Count >= maximumCount) break;
        }
        return results;
    }

    public static string NormalizeTimeZoneId(string timeZoneId)
    {
        _ = ResolveTimeZone(timeZoneId);
        return timeZoneId.Trim();
    }

    private static IEnumerable<DateTime> EnumerateLocalTimes(string unit, int interval, IReadOnlyCollection<DayOfWeek> daysOfWeek, int? dayOfMonth, DateTime start, DateTime? end)
    {
        return unit switch
        {
            ProjectManagementTaskRecurrenceFrequencies.Daily => EnumerateDaily(start, end, interval),
            ProjectManagementTaskRecurrenceFrequencies.Weekly => EnumerateWeekly(start, end, interval, daysOfWeek),
            ProjectManagementTaskRecurrenceFrequencies.Monthly => EnumerateMonthly(start, end, interval, dayOfMonth ?? start.Day),
            _ => throw new ValidationException("不支持的重复单位")
        };
    }

    private static IEnumerable<DateTime> EnumerateDaily(DateTime start, DateTime? end, int interval)
    {
        for (var value = start; !end.HasValue || value <= end.Value; value = value.AddDays(interval)) yield return value;
    }

    private static IEnumerable<DateTime> EnumerateWeekly(DateTime start, DateTime? end, int interval, IReadOnlyCollection<DayOfWeek> days)
    {
        DayOfWeek[] selected = days.Count == 0
            ? [start.DayOfWeek]
            : days.Distinct().OrderBy(value => (7 + (int)value - (int)DayOfWeek.Monday) % 7).ToArray();
        var weekStart = start.Date.AddDays(-((7 + (int)start.DayOfWeek - (int)DayOfWeek.Monday) % 7));
        for (var week = 0; ; week++)
        {
            var anchor = weekStart.AddDays(week * 7);
            if (end.HasValue && anchor.Date > end.Value.Date) yield break;
            if (week % interval != 0) continue;
            foreach (var day in selected)
            {
                var candidateDate = anchor.AddDays(((7 + (int)day - (int)DayOfWeek.Monday) % 7));
                var candidate = candidateDate.Add(start.TimeOfDay);
                if (candidate < start) continue;
                if (end.HasValue && candidate > end.Value) yield break;
                yield return candidate;
            }
        }
    }

    private static IEnumerable<DateTime> EnumerateMonthly(DateTime start, DateTime? end, int interval, int requestedDay)
    {
        var month = new DateTime(start.Year, start.Month, 1, start.Hour, start.Minute, start.Second, DateTimeKind.Unspecified);
        for (var offset = 0; ; offset += interval)
        {
            var candidateMonth = month.AddMonths(offset);
            var candidate = new DateTime(candidateMonth.Year, candidateMonth.Month, Math.Min(requestedDay, DateTime.DaysInMonth(candidateMonth.Year, candidateMonth.Month)), start.Hour, start.Minute, start.Second, DateTimeKind.Unspecified);
            if (candidate < start) continue;
            if (end.HasValue && candidate > end.Value) yield break;
            yield return candidate;
        }
    }

    private static DateTime ConvertLocalToUtc(DateTime localTime, TimeZoneInfo zone)
    {
        var effective = localTime;
        // 无效挂钟时间（DST 春季跳变）顺延到该时区下第一个真实分钟，确保不会丢失实例。
        while (zone.IsInvalidTime(effective)) effective = effective.AddMinutes(1);
        var offset = zone.IsAmbiguousTime(effective)
            ? zone.GetAmbiguousTimeOffsets(effective).Max()
            : zone.GetUtcOffset(effective);
        return new DateTimeOffset(effective, offset).UtcDateTime;
    }

    private static string BuildRecurrenceKey(DateTime localTime) => localTime.ToString("yyyyMMdd'T'HHmmss", global::System.Globalization.CultureInfo.InvariantCulture);
    private static DateTime AsUnspecified(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    private static string NormalizeUnit(string frequency, string? customUnit)
    {
        var value = (frequency ?? string.Empty).Trim();
        if (string.Equals(value, ProjectManagementTaskRecurrenceFrequencies.Custom, StringComparison.OrdinalIgnoreCase)) value = (customUnit ?? string.Empty).Trim();
        return value.ToUpperInvariant() switch
        {
            "DAILY" => ProjectManagementTaskRecurrenceFrequencies.Daily,
            "WEEKLY" => ProjectManagementTaskRecurrenceFrequencies.Weekly,
            "MONTHLY" => ProjectManagementTaskRecurrenceFrequencies.Monthly,
            _ => throw new ValidationException("重复频率必须为 Daily、Weekly、Monthly 或 Custom")
        };
    }

    private static void ValidateWeeklyDays(string unit, IReadOnlyCollection<DayOfWeek> days) { if (unit != ProjectManagementTaskRecurrenceFrequencies.Weekly || days.Count == 0) return; if (days.Any(day => !Enum.IsDefined(day))) throw new ValidationException("每周重复日无效"); }
    private static void ValidateMonthDay(string unit, int? day) { if (unit == ProjectManagementTaskRecurrenceFrequencies.Monthly && day is < 1 or > 31) throw new ValidationException("每月重复日期必须在 1 到 31 之间"); }
    private static TimeZoneInfo ResolveTimeZone(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ValidationException("时区不能为空"); try { return TimeZoneInfo.FindSystemTimeZoneById(value.Trim()); } catch (TimeZoneNotFoundException) { throw new ValidationException("时区不存在"); } catch (InvalidTimeZoneException) { throw new ValidationException("时区无效"); } }
}

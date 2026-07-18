using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementTaskRecurrenceScheduleCalculatorTests
{
    [Fact]
    public void Monthly_rule_clamps_day_31_to_month_end()
    {
        var schedules = Expand(ProjectManagementTaskRecurrenceFrequencies.Monthly, new DateTime(2025, 1, 31, 9, 0, 0), dayOfMonth: 31, until: new DateTime(2025, 4, 1));

        Assert.Equal([new DateTime(2025, 1, 31, 9, 0, 0), new DateTime(2025, 2, 28, 9, 0, 0), new DateTime(2025, 3, 31, 9, 0, 0)], schedules.Select(item => item.LocalTime));
    }

    [Fact]
    public void Monthly_rule_preserves_leap_day_semantics()
    {
        var schedules = Expand(ProjectManagementTaskRecurrenceFrequencies.Monthly, new DateTime(2024, 2, 29, 8, 0, 0), dayOfMonth: 29, until: new DateTime(2025, 4, 1));

        Assert.Contains(schedules, item => item.LocalTime == new DateTime(2024, 2, 29, 8, 0, 0));
        Assert.Contains(schedules, item => item.LocalTime == new DateTime(2025, 2, 28, 8, 0, 0));
    }

    [Fact]
    public void Weekly_rule_generates_selected_days_in_each_interval_week()
    {
        var schedules = Expand(ProjectManagementTaskRecurrenceFrequencies.Weekly, new DateTime(2025, 1, 6, 9, 0, 0), [DayOfWeek.Monday, DayOfWeek.Friday], until: new DateTime(2025, 1, 18));

        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Friday, DayOfWeek.Monday, DayOfWeek.Friday], schedules.Select(item => item.LocalTime.DayOfWeek));
        Assert.Equal(schedules.Count, schedules.Select(item => item.RecurrenceKey).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Custom_rule_uses_its_declared_unit_and_interval()
    {
        var schedules = ProjectManagementTaskRecurrenceScheduleCalculator.Expand(
            "custom", ProjectManagementTaskRecurrenceFrequencies.Custom, 2, [], null, ProjectManagementTaskRecurrenceFrequencies.Daily,
            new DateTime(2025, 1, 1, 9, 0, 0), null, "UTC",
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 7, 0, 0, 0, DateTimeKind.Utc), 10);

        Assert.Equal([1, 3, 5], schedules.Select(item => item.LocalTime.Day));
    }

    [Fact]
    public void Dst_invalid_local_time_is_shifted_to_first_real_instant()
    {
        var timeZone = FindNewYorkTimeZone();
        var schedules = ProjectManagementTaskRecurrenceScheduleCalculator.Expand(
            "rule-dst", ProjectManagementTaskRecurrenceFrequencies.Daily, 1, [], null, null,
            new DateTime(2024, 3, 10, 2, 30, 0), null, timeZone.Id,
            new DateTime(2024, 3, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 3, 10, 12, 0, 0, DateTimeKind.Utc), 10);

        Assert.Single(schedules);
        Assert.Equal(new DateTime(2024, 3, 10, 7, 0, 0, DateTimeKind.Utc), schedules[0].UtcTime);
    }

    private static IReadOnlyList<ProjectManagementTaskRecurrenceSchedule> Expand(string frequency, DateTime start, IReadOnlyCollection<DayOfWeek>? days = null, int? dayOfMonth = null, DateTime? until = null) =>
        ProjectManagementTaskRecurrenceScheduleCalculator.Expand("rule", frequency, 1, days ?? [], dayOfMonth, null, start, null, "UTC", start.ToUniversalTime(), (until ?? start.AddMonths(3)).ToUniversalTime(), 100);

    private static TimeZoneInfo FindNewYorkTimeZone()
    {
        foreach (var id in new[] { "Eastern Standard Time", "America/New_York" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }
        throw new InvalidOperationException("测试运行环境缺少纽约 DST 时区数据");
    }
}

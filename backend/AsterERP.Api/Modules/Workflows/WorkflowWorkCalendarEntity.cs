using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Workflows;

[SugarTable("workflow_work_calendars")]
public sealed class WorkflowWorkCalendarEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public DateTime CalendarDate { get; set; }

    public string DayType { get; set; } = "Workday";

    public bool IsWorkingDay { get; set; } = true;

    public string CalendarName { get; set; } = string.Empty;
}

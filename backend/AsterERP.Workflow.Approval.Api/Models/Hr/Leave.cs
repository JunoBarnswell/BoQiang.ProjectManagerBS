using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Hr;

[SqlSugar.SugarTable("tbl_hr_leave")]
public class Leave : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? ProcessInstanceId { get; set; }

    public string? ApplyerCode { get; set; }

    public string? Title { get; set; }

    public string? Type { get; set; }

    public float? Days { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public string? CompanyId { get; set; }

    public string? CompanyName { get; set; }

    public string? DeptId { get; set; }

    public string? DeptName { get; set; }

    public string? Note { get; set; }
}

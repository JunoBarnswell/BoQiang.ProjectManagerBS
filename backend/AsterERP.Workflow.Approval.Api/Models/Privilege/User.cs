using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Privilege;

[SqlSugar.SugarTable("tbl_privilege_user")]
public class User : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? RealName { get; set; }

    public string? UserNo { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? Tel { get; set; }

    public string? Mobile { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Image { get; set; }

    public string? CompanyId { get; set; }

    public string? DepartmentId { get; set; }

    public int? Sex { get; set; }

    public string? Address { get; set; }

    public string? Fax { get; set; }

    public int? FailMonth { get; set; }

    public DateTime? FailureTime { get; set; }

    public DateTime? PwdFtime { get; set; }

    public int? PwdInit { get; set; }

    public long? AclTimestamp { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? AppIds { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? DeptName { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string FailureTimeStr { get; set; } = "-";

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string PwdFtimeStr { get; set; } = "-";

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public int? FailFlag { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public List<Group>? Groups { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? CompanyName { get; set; }
}

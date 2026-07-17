using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Org;

[SqlSugar.SugarTable("tbl_org_personal")]
public class Personal : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? LeaderCode { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? LeaderName { get; set; }

    public string? HeadImg { get; set; }

    public string? Mobile { get; set; }

    public string? Email { get; set; }

    public string? DeptId { get; set; }

    public string? DeptName { get; set; }

    public string? CompanyId { get; set; }

    public string? CompanyName { get; set; }

    public int Status { get; set; } = 1;

    public DateTime? LeaveDate { get; set; }

    public string? ThirdParty { get; set; }

    public string? ThirdUserId { get; set; }

    public string? ThirdUnionId { get; set; }

    public string? DdUserid { get; set; }

    public int Sex { get; set; }

    public string? Address { get; set; }

    public string? Fax { get; set; }

    public string? PositionCode { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? PositionName { get; set; }

    public string? JobGradeCode { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? JobGradeName { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? DeptCode { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? CompanyCode { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public List<Role>? Roles { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public List<string>? CompanyIds { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public List<string>? DeptIds { get; set; }
}

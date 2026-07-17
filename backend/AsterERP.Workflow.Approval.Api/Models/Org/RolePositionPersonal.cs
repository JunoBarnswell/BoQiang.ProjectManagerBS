using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Org;

[SqlSugar.SugarTable("tbl_org_role_position_personal")]
public class RolePositionPersonal : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? CompanyId { get; set; }

    public string? RoleId { get; set; }

    public string? PositionCode { get; set; }

    public string? PersonalId { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? RoleNem { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? PositionName { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? PersonalName { get; set; }
}

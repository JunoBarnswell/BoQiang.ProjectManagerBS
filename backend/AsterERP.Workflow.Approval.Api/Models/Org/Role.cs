using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Org;

[SqlSugar.SugarTable("tbl_org_role")]
public class Role : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? CompanyId { get; set; }

    public string? PositionId { get; set; }

    public string? Name { get; set; }

    public int Type { get; set; } = 0;

    public string? Sn { get; set; }

    public string? Note { get; set; }

    public int OrderNo { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? CompanyName { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? PersonalId { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public List<Company>? Companies { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public List<Personal>? Personals { get; set; }
}

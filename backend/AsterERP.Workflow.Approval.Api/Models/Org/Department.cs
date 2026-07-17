using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Org;

[SqlSugar.SugarTable("tbl_org_department")]
public class Department : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? CompanyId { get; set; }

    public string? Name { get; set; }

    public string? Code { get; set; }

    public string? Note { get; set; }

    public string? Pid { get; set; }

    public int? OrderNo { get; set; }

    public string? LeaderCode { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? LeaderName { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? Pcode { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? CompanyCode { get; set; }

    public string? SuperiorCode { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? SuperiorName { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public List<string>? CompanyIds { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? CompanyName { get; set; }
}

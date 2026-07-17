using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Org;

[SqlSugar.SugarTable("tbl_org_company")]
public class Company : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? TypeId { get; set; }

    public string? Pid { get; set; }

    public string? Cname { get; set; }

    public string? ShortName { get; set; }

    public string? Ename { get; set; }

    public string? Code { get; set; }

    public string? Descr { get; set; }

    public int OrderNo { get; set; }

    public int? Status { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? Pcode { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? UserName { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public List<string>? RoleSnList { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public List<string>? CompanyIds { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? TypeCode { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? TypeName { get; set; }
}

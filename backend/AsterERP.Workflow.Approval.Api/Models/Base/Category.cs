using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Base;

[SqlSugar.SugarTable("tbl_base_category")]
public class Category : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? Pid { get; set; }

    public string? Name { get; set; }

    public string? Code { get; set; }

    public int? FrontShow { get; set; }

    public string? ShortName { get; set; }

    public string? Note { get; set; }

    public int? OrderNo { get; set; }

    public string? CompanyId { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? CreateTimeStr { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? UpdateTimeStr { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? CName { get; set; }
}

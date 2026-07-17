using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Base;

[SqlSugar.SugarTable("tbl_base_dic_item")]
public class DicItem : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? Code { get; set; }

    public string? Cname { get; set; }

    public string? Ename { get; set; }

    public string? MainId { get; set; }

    public int? OrderNo { get; set; }
}

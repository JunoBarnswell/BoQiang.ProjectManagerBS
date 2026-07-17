using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Base;

[SqlSugar.SugarTable("tbl_base_dictionary")]
public class Dictionary : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? DicTypeId { get; set; }

    public string? Code { get; set; }

    public string? Ename { get; set; }

    public string? Cname { get; set; }

    public string? Remark { get; set; }
}

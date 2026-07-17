using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Base;

[SqlSugar.SugarTable("tbl_base_dic_type")]
public class DicType : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Pid { get; set; }

    public string? Code { get; set; }

    public int? OrderNo { get; set; }
}

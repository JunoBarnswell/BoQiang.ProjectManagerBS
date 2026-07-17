using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Base;

[SqlSugar.SugarTable("tbl_base_area")]
public class Area : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Code { get; set; }

    public string? Pcode { get; set; }

    public string? Name { get; set; }
}

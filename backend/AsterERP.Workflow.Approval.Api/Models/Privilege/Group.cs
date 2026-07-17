using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Privilege;

[SqlSugar.SugarTable("tbl_privilege_group")]
public class Group : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Sn { get; set; }

    public string? Note { get; set; }

    public int? ValidState { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public List<User>? Users { get; set; }
}

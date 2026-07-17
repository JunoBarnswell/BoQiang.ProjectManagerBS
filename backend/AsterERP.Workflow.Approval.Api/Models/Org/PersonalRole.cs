using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Org;

[SqlSugar.SugarTable("tbl_org_personal_role")]
public class PersonalRole : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? PersonalId { get; set; }

    public string? PersonalCode { get; set; }

    public string? RoleId { get; set; }

    public DateTime? EndDate { get; set; }

    public int ValidMonth { get; set; }
}

using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Privilege;

[SqlSugar.SugarTable("tbl_privilege_user_group")]
public class UserGroup : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? UserId { get; set; }

    public string? UserNo { get; set; }

    public string? GroupId { get; set; }

    public int? ValidMonth { get; set; }

    public DateTime? EndDate { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? LongName { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? GroupSn { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? OwnCompany { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public List<UserGroup>? SaveRows { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? SaveRowsJson { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? EndDateStr { get; set; }
}

using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Workflow;

[SqlSugar.SugarTable("tbl_flow_extend_procinst")]
public class ExtendProcinst : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? ProcessInstanceId { get; set; }

    public string? ProcessDefinitionId { get; set; }

    public string? BusinessKey { get; set; }

    public string? ModelKey { get; set; }

    public string? ProcessName { get; set; }

    public string? ProcessStatus { get; set; }

    public string? CurrentUserCode { get; set; }

    public string? TenantId { get; set; }

    public string? UserInfo { get; set; }

    public string? FormData { get; set; }
}

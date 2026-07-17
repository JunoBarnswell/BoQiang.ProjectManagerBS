namespace AsterERP.Workflow.Approval.Api.ViewModels.Workflow.ProcessInstance;

public class InstanceQueryParamsVo
{
    public string UserCode { get; set; }
    public string AppSn { get; set; }
    public string StartTime { get; set; }
    public string EndTime { get; set; }
    public string BusinessKey { get; set; }
    public int OrderFlag { get; set; } = 0;
    public string ProcessInstanceId { get; set; }
    public string StartedUserIds { get; set; }
    public string ProcessDefinitionKey { get; set; }
    public string ProcessType { get; set; }
    public string DeptId { get; set; }
    public string CompanyId { get; set; }
    public string Keyword { get; set; }
}

namespace AsterERP.Workflow.Approval.Api.ViewModels.Workflow.ProcessInstance;

public class StartorBaseInfoVo
{
    public string ProcessInstanceId { get; set; }
    public string ModelKey { get; set; }
    public string ModelName { get; set; }
    public string BusinessKey { get; set; }
    public string FormName { get; set; }
    public Dictionary<string, object> StarterInfo { get; set; }
    public DateTime? CreateTime { get; set; }
}

namespace AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Runtime;

public class StartProcessInstanceVo
{
    public string ProcessDefinitionKey { get; set; }
    public string BusinessKey { get; set; }
    public Dictionary<string, object> Variables { get; set; }
    public string CurrentUserCode { get; set; }
    public string AppSn { get; set; }
    public string FormName { get; set; }
    public string Creator { get; set; }
    public string OldProcessInstanceId { get; set; }
    public bool FlowLevelFlag { get; set; } = true;
    public string DeptId { get; set; }
    public string FormData { get; set; }
}

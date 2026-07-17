namespace AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Comment;

public class FlowCommentVo
{
    protected string TaskId { get; set; }
    protected string PersonalCode { get; set; }
    protected string UserName { get; set; }
    protected string UserUrl { get; set; }
    protected string ProcessInstanceId { get; set; }
    protected string Message { get; set; }
    protected DateTime? Time { get; set; }
    public string Type { get; set; }
    public string TypeName { get; set; }
    public string TaskName { get; set; }
    public string FullMsg { get; set; }

    public FlowCommentVo() { }

    public FlowCommentVo(string personalCode, string processInstanceId, string message, string type)
    {
        PersonalCode = personalCode;
        ProcessInstanceId = processInstanceId;
        Message = message;
        Type = type;
    }

    public FlowCommentVo(string taskId, string personalCode, string processInstanceId, string message, string type)
    {
        TaskId = taskId;
        PersonalCode = personalCode;
        ProcessInstanceId = processInstanceId;
        Message = message;
        Type = type;
    }
}

using AsterERP.Workflow.Approval.Api.Enums.Workflow.Runtime;

namespace AsterERP.Workflow.Approval.Api.ViewModels.Workflow;

public abstract class BaseProcessVo
{
    public string TaskId { get; set; }
    public string ActivityId { get; set; }
    public string ActivityName { get; set; }
    public ProcessStatusEnum ProcessStatusEnum { get; set; }
    public string UserCode { get; set; }
    public string Message { get; set; }
    public CommentTypeEnum CommentTypeEnum { get; set; }
    public string ProcessInstanceId { get; set; }
}

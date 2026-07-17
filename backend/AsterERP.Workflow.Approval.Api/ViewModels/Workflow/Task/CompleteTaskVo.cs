using AsterERP.Workflow.Approval.Api.ViewModels.Extension.UserTask;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow;

namespace AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Task;

public class CompleteTaskVo : BaseProcessVo
{
    public string FormTitle { get; set; }
    public Dictionary<string, object> Variables { get; set; }
    public NextSequenceUserVo NextSequenceFlow { get; set; }
    public List<NextSequenceUserVo> NextUsers { get; set; }
}

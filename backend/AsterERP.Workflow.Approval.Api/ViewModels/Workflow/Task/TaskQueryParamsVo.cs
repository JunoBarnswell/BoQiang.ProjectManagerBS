using AsterERP.Workflow.Tools.Pager;

namespace AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Task;

public class TaskQueryParamsVo
{
    public string UserCode { get; set; }
    public string AppSn { get; set; }
    public List<string> AppSns { get; set; }
    public string FormName { get; set; }
    public string StartTime { get; set; }
    public string EndTime { get; set; }
    public string BusinessKey { get; set; }
    public string Assignee { get; set; }
    public int OrderFlag { get; set; } = 0;
    public string ProcessInstanceId { get; set; }
    public string ModelKey { get; set; }
    public int? FlowStatus { get; set; }
    public string Keyword { get; set; }
    public string TaskName { get; set; }
    public Dictionary<string, OrderBy> OrderbyMap { get; set; }
}

namespace AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Task;

public class TaskVo
{
    public string BusinessKey { get; set; }
    public string Name { get; set; }
    public string TaskId { get; set; }
    public string TaskDefKey { get; set; }
    public string Assignee { get; set; }
    public string AssigneeName { get; set; }
    public DateTime? CreateTime { get; set; }
    public string StayHour { get; set; }
    public string ProcessInstanceId { get; set; }
    public string ParentId { get; set; }
    public string ProcessDefinitionId { get; set; }
    public string ProcessDefinitionKey { get; set; }
    public int? ProcessDefinitionType { get; set; }
    public int? FormType { get; set; }
    public string ProcessStatus { get; set; }
    public string ProcessStatusName { get; set; }
    public string TaskType { get; set; }
    public int Status { get; set; } = 0;
    public string UserId { get; set; }
    public string UserName { get; set; }
    public List<string> GroupIds { get; set; }
    public bool Finished { get; set; } = false;
    public DateTime? FinishedTime { get; set; }
    public string FormName { get; set; }
    public string StartPersonCode { get; set; }
    public string StartPersonName { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string AppName { get; set; }
    public string TotalTime { get; set; }
    public string BusinessUrl { get; set; }
    public bool CandidateFlag { get; set; } = false;
    public int Type { get; set; } = 0;
    public string CurrentAssignees { get; set; }
    public string CurrentAssigneeNos { get; set; }
}

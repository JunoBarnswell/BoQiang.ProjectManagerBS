using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Task;

namespace AsterERP.Workflow.Approval.Api.ViewModels.Workflow.ProcessInstance;

public class ProcessInstanceVo
{
    public string ProcessInstanceId { get; set; }
    public string ProcessDefinitionId { get; set; }
    public string ProcessDefinitionName { get; set; }
    public string ProcessDefinitionKey { get; set; }
    public int? ProcessDefinitionType { get; set; }
    public int? FormType { get; set; }
    public int? ProcessDefinitionVersion { get; set; }
    public string CategoryCode { get; set; }
    public string DeploymentId { get; set; }
    public string BusinessKey { get; set; }
    public string Assignees { get; set; }
    public string AppId { get; set; }
    public string AppSn { get; set; }
    public DateTime? CreateTime { get; set; }
    public bool? PState { get; set; }
    public string Reason { get; set; }
    public string StartedUserId { get; set; }
    public string StartedUserName { get; set; }
    public List<string> StartedUserIds { get; set; }
    public string StartedUserDept { get; set; }
    public string StartedUserDeptName { get; set; }
    public string StartedUserCom { get; set; }
    public string StartedUserComName { get; set; }
    public bool FinishFlag { get; set; } = false;
    public string ProcessStatus { get; set; }
    public string ProcessStatusName { get; set; }
    public string FormName { get; set; }
    public string StartPersonName { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string StartTimeStr { get; set; }
    public string EndTimeStr { get; set; }
    public string AppName { get; set; }
    public string CurrentNodeName { get; set; }
    public string BusinessUrl { get; set; }
    public string TotalTime { get; set; }
    public List<ApproverVo> CurrentAssignees { get; set; }
    public int? QueryType { get; set; }
    public string NewVersion { get; set; }
    public List<string> ProInstanceIdList { get; set; }
    public string TableName { get; set; }

    public ProcessInstanceVo() { }

    public ProcessInstanceVo(string processInstanceId, string businessKey, string formName, string startedUserId)
    {
        ProcessInstanceId = processInstanceId;
        BusinessKey = businessKey;
        FormName = formName;
        StartedUserId = startedUserId;
    }
}

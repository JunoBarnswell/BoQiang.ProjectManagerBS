using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Workflow;

[SqlSugar.SugarTable("tbl_flow_comment_info")]
public class CommentInfo : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? Type { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? TypeName { get; set; }

    public string? PersonalCode { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? PersonalName { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? PersonalHeadImg { get; set; }

    public DateTime? Time { get; set; }

    public string? TaskId { get; set; }

    public string? ActivityId { get; set; }

    public string? ActivityName { get; set; }

    public string? ProcessInstanceId { get; set; }

    public string? Action { get; set; }

    public string? Message { get; set; }

    public CommentInfo() { }

    public CommentInfo(string? type, string? personalCode, string? processInstanceId, string? message)
    {
        Type = type;
        PersonalCode = personalCode;
        ProcessInstanceId = processInstanceId;
        Message = message;
    }

    public CommentInfo(string? type, string? personalCode, string? taskId, string? processInstanceId, string? message)
    {
        Type = type;
        PersonalCode = personalCode;
        TaskId = taskId;
        ProcessInstanceId = processInstanceId;
        Message = message;
    }
}

namespace AsterERP.Workflow.Approval.Api.Models.Privilege;

[SqlSugar.SugarTable("tbl_privilege_login_log")]
public class LoginLog
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? Ip { get; set; }

    public string? OperationId { get; set; }

    public string? OperationUsername { get; set; }

    public string? OperationPerson { get; set; }

    public string? OperationContent { get; set; }

    public DateTime? OperationTime { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? OperationTimeStr { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? StartTimeStr { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? EndTimeStr { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? Keyword { get; set; }

    public LoginLog() { }

    public LoginLog(string? ip, string? operationId, string? operationUsername, string? operationPerson, string? operationContent)
    {
        Ip = ip;
        OperationId = operationId;
        OperationUsername = operationUsername;
        OperationPerson = operationPerson;
        OperationContent = operationContent;
    }
}

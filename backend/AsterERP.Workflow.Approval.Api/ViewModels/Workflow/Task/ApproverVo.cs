namespace AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Task;

public class ApproverVo
{
    public const string ROLE = "role";
    public const string USER = "user";

    public string Type { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Mobile { get; set; }

    public ApproverVo() { }

    public ApproverVo(string type, string code, string name, string mobile)
    {
        Type = type;
        Code = code;
        Name = name;
        Mobile = mobile;
    }
}

namespace AsterERP.Workflow.Approval.Api.ViewModels.Extension.UserTask;

public class NextSequenceUserVo
{
    public const string SEQUENCE_KEY = "sequence";
    public const string USER_KEY = "user";

    public string Code { get; set; }
    public string Name { get; set; }
    public bool Multiple { get; set; }
    public string Values { get; set; }
}

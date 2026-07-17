namespace AsterERP.Workflow.Approval.Api.Enums.Workflow.Listener;

public enum ListenerTypeEnum
{
    ExecutionListener,
    TaskListener
}

public static class ListenerTypeEnumExtensions
{
    private static readonly Dictionary<ListenerTypeEnum, (string Type, string Msg)> _map = new()
    {
        [ListenerTypeEnum.ExecutionListener] = ("executionListener", "任务监听"),
        [ListenerTypeEnum.TaskListener] = ("taskListener", "执行监听")
    };

    public static string GetType(this ListenerTypeEnum value) => _map[value].Type;
    public static string GetMsg(this ListenerTypeEnum value) => _map[value].Msg;
}

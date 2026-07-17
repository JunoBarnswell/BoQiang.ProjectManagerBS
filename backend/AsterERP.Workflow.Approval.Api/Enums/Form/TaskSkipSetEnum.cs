namespace AsterERP.Workflow.Approval.Api.Enums.Form;

public enum TaskSkipSetEnum
{
    NoSkip,
    AdjoinSkip,
    RepeatSkip
}

public static class TaskSkipSetEnumExtensions
{
    private static readonly Dictionary<TaskSkipSetEnum, (int Code, string Message)> _map = new()
    {
        [TaskSkipSetEnum.NoSkip] = (1, "不跳过"),
        [TaskSkipSetEnum.AdjoinSkip] = (2, "相邻任务节点跳过"),
        [TaskSkipSetEnum.RepeatSkip] = (3, "重复任务跳过")
    };

    public static int GetCode(this TaskSkipSetEnum value) => _map[value].Code;
    public static string GetMessage(this TaskSkipSetEnum value) => _map[value].Message;

    public static Dictionary<string, string> GetMap()
    {
        var map = new Dictionary<string, string>();
        foreach (var kvp in _map)
            map[kvp.Value.Code.ToString()] = kvp.Value.Message;
        return map;
    }

    public static List<Dictionary<string, string>> GetAllInfo()
    {
        var list = new List<Dictionary<string, string>>();
        foreach (var kvp in _map)
        {
            list.Add(new Dictionary<string, string>
            {
                ["code"] = kvp.Value.Code.ToString(),
                ["message"] = kvp.Value.Message
            });
        }
        return list;
    }
}

namespace AsterERP.Workflow.Approval.Api.Enums.Workflow.Task;

public enum TaskTypeEnum
{
    ZB,
    QJQ,
    HJQ
}

public static class TaskTypeEnumExtensions
{
    private static readonly Dictionary<TaskTypeEnum, string> _typeMap = new()
    {
        [TaskTypeEnum.ZB] = "转办",
        [TaskTypeEnum.QJQ] = "前加签",
        [TaskTypeEnum.HJQ] = "后加签"
    };

    public static string GetType(this TaskTypeEnum value) => _typeMap[value];
}

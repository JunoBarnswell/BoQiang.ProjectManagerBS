namespace AsterERP.Workflow.Approval.Api.Enums.Workflow.Task;

public enum NodeStatusEnum
{
    Pending,
    Processing,
    Finish
}

public static class NodeStatusEnumExtensions
{
    private static readonly Dictionary<NodeStatusEnum, (string Type, string Description)> _map = new()
    {
        [NodeStatusEnum.Pending] = ("pending", "待处理"),
        [NodeStatusEnum.Processing] = ("processing", "处理中"),
        [NodeStatusEnum.Finish] = ("finish", "已处理")
    };

    public static string GetType(this NodeStatusEnum value) => _map[value].Type;
    public static string GetDescription(this NodeStatusEnum value) => _map[value].Description;
}

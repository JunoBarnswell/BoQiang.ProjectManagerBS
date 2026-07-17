namespace AsterERP.Workflow.Approval.Api.Enums.Workflow.Task;

public enum NodeTypeEnum
{
    Notify,
    Apply,
    Noapprove,
    Bs,
    Coordination,
    Review,
    Applying
}

public static class NodeTypeEnumExtensions
{
    private static readonly Dictionary<NodeTypeEnum, (string Type, string Description)> _map = new()
    {
        [NodeTypeEnum.Notify] = ("notify", "知会"),
        [NodeTypeEnum.Apply] = ("apply", "审批"),
        [NodeTypeEnum.Noapprove] = ("noapprove", "不审批"),
        [NodeTypeEnum.Bs] = ("bs", "必审"),
        [NodeTypeEnum.Coordination] = ("coordination", "协同"),
        [NodeTypeEnum.Review] = ("review", "评审"),
        [NodeTypeEnum.Applying] = ("applying", "审批中")
    };

    public static string GetType(this NodeTypeEnum value) => _map[value].Type;
    public static string GetDescription(this NodeTypeEnum value) => _map[value].Description;
}

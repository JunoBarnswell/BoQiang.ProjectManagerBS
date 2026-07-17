namespace AsterERP.Workflow.Approval.Api.Enums.Workflow.Listener;

public enum ListenerValueTypeEnum
{
    Class,
    Expression,
    DelegateExpression
}

public static class ListenerValueTypeEnumExtensions
{
    private static readonly Dictionary<ListenerValueTypeEnum, (string Type, string Msg)> _map = new()
    {
        [ListenerValueTypeEnum.Class] = ("class", "类"),
        [ListenerValueTypeEnum.Expression] = ("expression", "表达式"),
        [ListenerValueTypeEnum.DelegateExpression] = ("delegateExpression", "代理表达式")
    };

    public static string GetType(this ListenerValueTypeEnum value) => _map[value].Type;
    public static string GetMsg(this ListenerValueTypeEnum value) => _map[value].Msg;
}

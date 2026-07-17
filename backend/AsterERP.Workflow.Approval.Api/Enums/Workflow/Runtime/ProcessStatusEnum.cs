namespace AsterERP.Workflow.Approval.Api.Enums.Workflow.Runtime;

public enum ProcessStatusEnum
{
    CG,
    SPZ,
    BH,
    CH,
    JQ,
    ZC,
    ZB,
    BJ,
    ZZ
}

public static class ProcessStatusEnumExtensions
{
    private static readonly Dictionary<ProcessStatusEnum, string> MsgMap = new()
    {
        { ProcessStatusEnum.CG, "草稿" },
        { ProcessStatusEnum.SPZ, "审批中" },
        { ProcessStatusEnum.BH, "驳回" },
        { ProcessStatusEnum.CH, "撤回" },
        { ProcessStatusEnum.JQ, "加签" },
        { ProcessStatusEnum.ZC, "暂存" },
        { ProcessStatusEnum.ZB, "转办" },
        { ProcessStatusEnum.BJ, "办结" },
        { ProcessStatusEnum.ZZ, "终止" }
    };

    public static string GetMsg(this ProcessStatusEnum e) => MsgMap.GetValueOrDefault(e, "");

    public static string GetEnumMsgByType(string type)
    {
        if (Enum.TryParse<ProcessStatusEnum>(type, out var e))
        {
            return e.GetMsg();
        }
        return "";
    }
}

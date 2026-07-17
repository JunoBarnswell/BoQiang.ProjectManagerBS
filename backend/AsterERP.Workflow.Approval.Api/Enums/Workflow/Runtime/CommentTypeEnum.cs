namespace AsterERP.Workflow.Approval.Api.Enums.Workflow.Runtime;

public enum CommentTypeEnum
{
    SP,
    QS,
    FQS,
    BH,
    CH,
    ZC,
    XTZX,
    TJ,
    CXTJ,
    SPJS,
    LCZZ,
    WP,
    ZH,
    ZY,
    YY,
    ZB,
    SQ,
    SPBJQ,
    HJQ,
    QJQ,
    CFTG,
    XT,
    PS
}

public static class CommentTypeEnumExtensions
{
    private static readonly Dictionary<CommentTypeEnum, string> NameMap = new()
    {
        { CommentTypeEnum.SP, "审批" },
        { CommentTypeEnum.QS, "签收" },
        { CommentTypeEnum.FQS, "反签收" },
        { CommentTypeEnum.BH, "驳回" },
        { CommentTypeEnum.CH, "撤回" },
        { CommentTypeEnum.ZC, "暂存" },
        { CommentTypeEnum.XTZX, "系统后台执行" },
        { CommentTypeEnum.TJ, "提交" },
        { CommentTypeEnum.CXTJ, "重新提交" },
        { CommentTypeEnum.SPJS, "审批结束" },
        { CommentTypeEnum.LCZZ, "流程终止" },
        { CommentTypeEnum.WP, "委派" },
        { CommentTypeEnum.ZH, "知会" },
        { CommentTypeEnum.ZY, "转阅" },
        { CommentTypeEnum.YY, "已阅" },
        { CommentTypeEnum.ZB, "转办" },
        { CommentTypeEnum.SQ, "授权" },
        { CommentTypeEnum.SPBJQ, "审批并加签" },
        { CommentTypeEnum.HJQ, "后加签" },
        { CommentTypeEnum.QJQ, "前加签" },
        { CommentTypeEnum.CFTG, "重复跳过" },
        { CommentTypeEnum.XT, "协同" },
        { CommentTypeEnum.PS, "评审" }
    };

    public static string GetName(this CommentTypeEnum e) => NameMap.GetValueOrDefault(e, "");

    public static string GetEnumMsgByType(string type)
    {
        if (Enum.TryParse<CommentTypeEnum>(type, out var e))
        {
            return e.GetName();
        }
        return "";
    }
}

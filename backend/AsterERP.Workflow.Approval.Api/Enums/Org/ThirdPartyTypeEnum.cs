namespace AsterERP.Workflow.Approval.Api.Enums.Org;

public enum ThirdPartyTypeEnum
{
    DingTalk,
    FeiShu,
    WeiXin
}

public static class ThirdPartyTypeEnumExtensions
{
    private static readonly Dictionary<ThirdPartyTypeEnum, (string Code, string Message)> _map = new()
    {
        [ThirdPartyTypeEnum.DingTalk] = ("DingTalk", "钉钉"),
        [ThirdPartyTypeEnum.FeiShu] = ("FeiShu", "飞书"),
        [ThirdPartyTypeEnum.WeiXin] = ("WeiXin", "企业微信")
    };

    public static string GetCode(this ThirdPartyTypeEnum value) => _map[value].Code;
    public static string GetMessage(this ThirdPartyTypeEnum value) => _map[value].Message;

    public static Dictionary<string, string> GetMap()
    {
        var map = new Dictionary<string, string>();
        foreach (var kvp in _map)
            map[kvp.Value.Code] = kvp.Value.Message;
        return map;
    }

    public static List<Dictionary<string, string>> GetAllInfo()
    {
        var list = new List<Dictionary<string, string>>();
        foreach (var kvp in _map)
        {
            list.Add(new Dictionary<string, string>
            {
                ["code"] = kvp.Value.Code,
                ["message"] = kvp.Value.Message
            });
        }
        return list;
    }
}

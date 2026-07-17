namespace AsterERP.Workflow.Approval.Api.Enums.Org;

public enum RoleTypeEnum
{
    Common,
    Company,
    Department
}

public static class RoleTypeEnumExtensions
{
    private static readonly Dictionary<RoleTypeEnum, (int Code, string Message)> _map = new()
    {
        [RoleTypeEnum.Common] = (0, "普通角色"),
        [RoleTypeEnum.Company] = (1, "公司矩阵角色"),
        [RoleTypeEnum.Department] = (2, "部门矩阵角色")
    };

    public static int GetCode(this RoleTypeEnum value) => _map[value].Code;
    public static string GetMessage(this RoleTypeEnum value) => _map[value].Message;

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

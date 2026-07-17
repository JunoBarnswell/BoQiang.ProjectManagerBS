namespace AsterERP.Workflow.Approval.Api.Enums.Workflow.Runtime;

public enum StartVariableEnum
{
    User,
    Udept,
    Ucompany,
    CompanyRole,
    MatrixCompanyRole,
    MatrixDeptRole,
    Form,
    Lcdb,
    Line
}

public static class StartVariableEnumExtensions
{
    private static readonly Dictionary<StartVariableEnum, (string Code, string Msg)> _map = new()
    {
        [StartVariableEnum.User] = ("user", "人员信息"),
        [StartVariableEnum.Udept] = ("udept", "人员部门变量"),
        [StartVariableEnum.Ucompany] = ("ucompany", "人员公司变量"),
        [StartVariableEnum.CompanyRole] = ("company", "公司角色层级审批领导"),
        [StartVariableEnum.MatrixCompanyRole] = ("mcompany", "矩阵公司角色"),
        [StartVariableEnum.MatrixDeptRole] = ("mdept", "矩阵部门角色"),
        [StartVariableEnum.Form] = ("form", "表单"),
        [StartVariableEnum.Lcdb] = ("lcdb", "流程底表"),
        [StartVariableEnum.Line] = ("line", "汇报线")
    };

    public static string GetCode(this StartVariableEnum value) => _map[value].Code;
    public static string GetMsg(this StartVariableEnum value) => _map[value].Msg;

    public static string GetEnumMsgByCode(string code)
    {
        foreach (var kvp in _map)
            if (kvp.Value.Code == code) return kvp.Value.Msg;
        return "";
    }
}

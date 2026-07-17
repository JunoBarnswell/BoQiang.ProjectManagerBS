namespace AsterERP.Workflow.Approval.Api.Enums.Privilege;

public enum SystemConfigEnum
{
    Logo,
    Favicon,
    LoginBg,
    SystemName
}

public static class SystemConfigEnumExtensions
{
    private static readonly Dictionary<SystemConfigEnum, string> _snMap = new()
    {
        [SystemConfigEnum.Logo] = "logo",
        [SystemConfigEnum.Favicon] = "favicon",
        [SystemConfigEnum.LoginBg] = "login_bg",
        [SystemConfigEnum.SystemName] = "system_name"
    };

    public static string GetSn(this SystemConfigEnum value) => _snMap[value];
}

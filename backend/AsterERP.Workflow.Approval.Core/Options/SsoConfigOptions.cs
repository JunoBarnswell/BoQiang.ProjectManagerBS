namespace AsterERP.Workflow.Approval.Core.Options;

public class SsoConfigOptions
{
    public const string SectionName = "AsterERP:Flow:Sso";

    public string SsoUrl { get; set; } = "http://172.24.100.107:14000/auth/oauth/check_token";
    public string SsoAppId { get; set; } = "0";
    public string SsoAuthorization { get; set; } = "Basic cGlnOnBpZw==";
}

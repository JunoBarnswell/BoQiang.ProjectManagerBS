namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed class InfrastructureEmailTestRequest
{
    public string To { get; set; } = string.Empty;

    public string Subject { get; set; } = "AsterERP 邮件配置测试";

    public string Body { get; set; } = "AsterERP 邮件基础设施配置测试。";

    public bool IsBodyHtml { get; set; }
}

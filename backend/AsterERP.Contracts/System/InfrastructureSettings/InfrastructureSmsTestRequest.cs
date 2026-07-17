namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed class InfrastructureSmsTestRequest
{
    public string PhoneNumber { get; set; } = string.Empty;

    public string Text { get; set; } = "AsterERP 短信配置测试";
}

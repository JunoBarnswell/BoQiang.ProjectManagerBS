namespace AsterERP.Contracts.Ai;

public sealed class AiSkCapabilityDto
{
    public string CapabilityCode { get; set; } = string.Empty;

    public string Status { get; set; } = "Blocked";

    public string FrameworkType { get; set; } = string.Empty;

    public string ImplementationSymbol { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}

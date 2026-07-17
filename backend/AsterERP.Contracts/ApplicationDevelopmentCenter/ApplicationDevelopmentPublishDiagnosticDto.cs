namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentPublishDiagnosticDto
{
    public string? ActionId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string? ElementId { get; set; }

    public string? FixHint { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? PageCode { get; set; }

    public string? PageId { get; set; }

    public string? Path { get; set; }

    public string Severity { get; set; } = "error";
}

namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowTestCaseDefinition
{
    public string TestCode { get; set; } = string.Empty;

    public string TestName { get; set; } = string.Empty;

    public Dictionary<string, object?> Variables { get; set; } = [];
}

namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record RuntimeMicroflowContractResponse(
    string FlowCode,
    string FlowName,
    IReadOnlyList<ApplicationMicroflowVariableDefinition> Inputs,
    IReadOnlyList<ApplicationMicroflowVariableDefinition> Outputs,
    int VersionNo);

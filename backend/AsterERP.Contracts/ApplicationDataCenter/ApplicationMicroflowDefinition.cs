namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowDefinition
{
    public int SchemaVersion { get; set; } = 1;

    public List<ApplicationMicroflowDomainObjectDefinition> DomainObjects { get; set; } = [];

    public List<ApplicationMicroflowAssociationDefinition> Associations { get; set; } = [];

    public List<ApplicationMicroflowVariableDefinition> Inputs { get; set; } = [];

    public List<ApplicationMicroflowVariableDefinition> Outputs { get; set; } = [];

    public List<ApplicationMicroflowVariableDefinition> Variables { get; set; } = [];

    public List<ApplicationMicroflowNodeDefinition> Nodes { get; set; } = [];

    public List<ApplicationMicroflowEdgeDefinition> Edges { get; set; } = [];

    public List<ApplicationMicroflowDataMappingDefinition> DataMappings { get; set; } = [];

    public List<ApplicationMicroflowApiEndpointDefinition> ApiEndpoints { get; set; } = [];

    public Dictionary<string, object?> Permissions { get; set; } = [];

    public List<ApplicationMicroflowTestCaseDefinition> TestCases { get; set; } = [];
}

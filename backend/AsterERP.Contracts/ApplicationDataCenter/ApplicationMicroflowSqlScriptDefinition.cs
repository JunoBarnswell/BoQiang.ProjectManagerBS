namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowSqlScriptDefinition
{
    public string DataSourceId { get; set; } = string.Empty;

    public List<ApplicationMicroflowSqlScriptLocalVariableDefinition> LocalVariables { get; set; } = [];

    public int MaxRows { get; set; } = 50;

    public List<ApplicationMicroflowSqlScriptParameterDefinition> Parameters { get; set; } = [];

    public ApplicationMicroflowSqlScriptResultShapeDefinition ResultShape { get; set; } = new();

    public string Script { get; set; } = string.Empty;
}

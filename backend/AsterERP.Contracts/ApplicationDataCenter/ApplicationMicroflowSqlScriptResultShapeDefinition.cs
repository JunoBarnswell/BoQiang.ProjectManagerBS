namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowSqlScriptResultShapeDefinition
{
    public List<ApplicationMicroflowFieldDefinition> Fields { get; set; } = [];

    public string ValueType { get; set; } = "array";
}

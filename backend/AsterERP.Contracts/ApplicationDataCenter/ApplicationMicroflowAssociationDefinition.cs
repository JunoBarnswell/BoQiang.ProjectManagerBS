namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowAssociationDefinition
{
    public string AssociationCode { get; set; } = string.Empty;

    public string SourceObjectCode { get; set; } = string.Empty;

    public string TargetObjectCode { get; set; } = string.Empty;

    public string Cardinality { get; set; } = "oneToMany";

    public string SourceKeyField { get; set; } = "id";

    public string TargetForeignKeyField { get; set; } = string.Empty;

    public bool CascadeDelete { get; set; }

    public string SaveMode { get; set; } = "append";

    public string DeleteMode { get; set; } = "cascade";

    public bool Required { get; set; }
}

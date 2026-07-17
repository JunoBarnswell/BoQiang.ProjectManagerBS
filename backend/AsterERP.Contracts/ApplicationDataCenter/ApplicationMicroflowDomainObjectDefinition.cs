namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowDomainObjectDefinition
{
    public string ObjectCode { get; set; } = string.Empty;

    public string ObjectName { get; set; } = string.Empty;

    public string ModelCode { get; set; } = string.Empty;

    public string IdGeneration { get; set; } = "guid";

    public string KeyField { get; set; } = "id";

    public List<ApplicationMicroflowFieldDefinition> Fields { get; set; } = [];
}

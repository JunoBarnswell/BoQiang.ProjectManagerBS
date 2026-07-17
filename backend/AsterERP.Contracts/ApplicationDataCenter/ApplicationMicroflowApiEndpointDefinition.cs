namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowApiEndpointDefinition
{
    public string EndpointCode { get; set; } = string.Empty;

    public string EndpointName { get; set; } = string.Empty;

    public string HttpMethod { get; set; } = "POST";

    public string RoutePath { get; set; } = string.Empty;

    public string? StartNodeId { get; set; }

    public string? PermissionCode { get; set; }

    public bool RequiresAuthentication { get; set; } = true;
}

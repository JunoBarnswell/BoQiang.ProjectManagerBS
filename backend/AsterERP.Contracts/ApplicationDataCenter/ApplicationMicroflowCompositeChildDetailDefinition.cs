using AsterERP.Contracts.Runtime;

namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowCompositeChildDetailDefinition
{
    public string ModelCode { get; set; } = string.Empty;

    public string ParentKeyField { get; set; } = "id";

    public string ForeignKeyField { get; set; } = string.Empty;

    public string? BindingKey { get; set; }

    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Keyword { get; set; }

    public List<RuntimeModelFilterMappingDto> Filters { get; set; } = [];
}

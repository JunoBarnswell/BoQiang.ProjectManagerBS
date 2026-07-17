using System.Text.Json.Serialization;
using AsterERP.Contracts.Runtime;
using AsterERP.Api.Modules.Runtime;

namespace AsterERP.Api.Application.Runtime;

public sealed record RuntimeDataModelDefinition(
    string Id,
    string TenantId,
    string AppCode,
    string ModelCode,
    string ModelName,
    string ProviderKey,
    string KeyField,
    string IdGeneration,
    string? PermissionCode,
    IReadOnlyList<RuntimeDataFieldDefinition> Fields,
    IReadOnlyDictionary<string, object?>? Source = null,
    IReadOnlyList<RuntimeModelOperationDefinitionDto>? Operations = null)
{
    public IReadOnlyList<RuntimeDataFieldResponse> ToFieldResponses() =>
        Fields
            .OrderBy(item => item.Order)
            .Select(item => new RuntimeDataFieldResponse(
                item.FieldCode,
                item.FieldName,
                item.DataType,
                item.Binding,
                item.Visible,
                item.Queryable,
                item.Sortable,
                item.Exportable,
                item.Writable,
                item.Renderer,
                item.DictType,
                item.Width,
                item.Fixed,
                item.Order,
                item.Required,
                item.DisplayHelpers,
                item.WriteHelpers,
                item.QueryHelpers))
            .ToList();

    public static RuntimeDataModelDefinition FromEntity(SystemDataModelEntity entity, RuntimeDataModelSchema schema) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.AppCode,
            entity.ModelCode,
            entity.ModelName,
            entity.ProviderKey,
            entity.KeyField,
            string.IsNullOrWhiteSpace(schema.IdGeneration) ? RuntimeModelIdGeneration.Guid : schema.IdGeneration,
            entity.PermissionCode,
            schema.Fields,
            schema.Source,
            schema.Operations);
}

public sealed class RuntimeDataFieldDefinition
{
    public string FieldCode { get; set; } = string.Empty;

    public string FieldName { get; set; } = string.Empty;

    public string DataType { get; set; } = "text";

    public string Binding { get; set; } = string.Empty;

    public bool Visible { get; set; } = true;

    public bool Queryable { get; set; }

    public bool Sortable { get; set; }

    public bool Exportable { get; set; }

    public bool Writable { get; set; }

    public bool Required { get; set; }

    public string? Renderer { get; set; }

    public string? DictType { get; set; }

    public string? Width { get; set; }

    public string? Fixed { get; set; }

    public int Order { get; set; }

    public List<RuntimeExpressionHelperDto> DisplayHelpers { get; set; } = [];

    public List<RuntimeExpressionHelperDto> WriteHelpers { get; set; } = [];

    public List<RuntimeExpressionHelperDto> QueryHelpers { get; set; } = [];
}

public sealed class RuntimeDataModelSchema
{
    [JsonPropertyName("idGeneration")]
    public string IdGeneration { get; set; } = RuntimeModelIdGeneration.Guid;

    [JsonPropertyName("source")]
    public Dictionary<string, object?>? Source { get; set; }

    [JsonPropertyName("fields")]
    public List<RuntimeDataFieldDefinition> Fields { get; set; } = [];

    [JsonPropertyName("operations")]
    public List<RuntimeModelOperationDefinitionDto> Operations { get; set; } = [];
}

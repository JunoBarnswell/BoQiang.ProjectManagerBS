namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentBusinessObjectFieldDto
{
    public string FieldCode { get; set; } = string.Empty;

    public string FieldName { get; set; } = string.Empty;

    public string DataType { get; set; } = "text";

    public string? Binding { get; set; }

    public bool Visible { get; set; } = true;

    public bool Queryable { get; set; } = true;

    public bool Sortable { get; set; } = true;

    public bool Exportable { get; set; } = true;

    public bool Writable { get; set; } = true;

    public bool Required { get; set; }

    public bool IsPrimaryKey { get; set; }

    public int Order { get; set; }
}

using AsterERP.Contracts.Runtime;

namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowFieldDefinition
{
    public string FieldCode { get; set; } = string.Empty;

    public string FieldName { get; set; } = string.Empty;

    public string DataType { get; set; } = "string";

    public bool Visible { get; set; } = true;

    public bool Writable { get; set; } = true;

    public bool Required { get; set; }

    public bool ReadOnly { get; set; }

    public RuntimeValueExpressionDto? Expression { get; set; }

    public List<RuntimeExpressionHelperDto> DisplayHelpers { get; set; } = [];

    public List<RuntimeExpressionHelperDto> WriteHelpers { get; set; } = [];

    public List<RuntimeExpressionHelperDto> QueryHelpers { get; set; } = [];
}

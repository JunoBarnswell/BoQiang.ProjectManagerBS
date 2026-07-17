namespace AsterERP.Workflow.Core.Impl;

public interface IDataObject
{
    string? Name { get; }
    string? LocalizedName { get; }
    string? Description { get; }
    object? Value { get; }
    string? Type { get; }
    string? DataObjectDefinitionKey { get; }
}

public class DataObjectImpl : IDataObject
{
    public string? Name { get; set; }
    public object? Value { get; set; }
    public string? Description { get; set; }
    public string? LocalizedName { get; set; }
    public string? LocalizedDescription { get; set; }
    public string? DataObjectDefinitionKey { get; set; }
    public string? Type { get; set; }

    public DataObjectImpl() { }

    public DataObjectImpl(
        string? name,
        object? value,
        string? description,
        string? type,
        string? localizedName,
        string? localizedDescription,
        string? dataObjectDefinitionKey)
    {
        Name = name;
        Value = value;
        Type = type;
        Description = description;
        LocalizedName = localizedName;
        LocalizedDescription = localizedDescription;
        DataObjectDefinitionKey = dataObjectDefinitionKey;
    }

    string? IDataObject.LocalizedName => !string.IsNullOrEmpty(LocalizedName) ? LocalizedName : Name;

    string? IDataObject.Description => !string.IsNullOrEmpty(LocalizedDescription) ? LocalizedDescription : Description;
}

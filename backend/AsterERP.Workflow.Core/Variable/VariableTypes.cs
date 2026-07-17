namespace AsterERP.Workflow.Core.Variable;

public class VariableTypes
{
    private readonly List<IVariableType> _types = new();
    private readonly Dictionary<string, IVariableType> _typesMap = new();

    public void AddType(IVariableType type)
    {
        _types.Add(type);
        _typesMap[type.TypeName] = type;
    }

    public IVariableType? GetVariableType(string typeName)
    {
        return _typesMap.GetValueOrDefault(typeName);
    }

    public IVariableType FindVariableType(object? value)
    {
        if (value == null)
        {
            return _types.FirstOrDefault(t => t is NullType) ?? new NullType();
        }

        foreach (var type in _types)
        {
            if (type.IsAbleToStore(value))
            {
                return type;
            }
        }

        return new SerializableType();
    }

    public int Size => _types.Count;
}

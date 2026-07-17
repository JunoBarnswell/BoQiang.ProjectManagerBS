using System;

namespace AsterERP.Workflow.BpmnModel;

public static class BpmnDataObjects
{
    public static ValuedDataObject CreateDataObject(string? structureRef)
    {
        if (string.IsNullOrEmpty(structureRef))
            return new StringDataObject();

        var typeName = structureRef!;
        var colonIndex = typeName.IndexOf(':');
        if (colonIndex >= 0)
            typeName = typeName.Substring(colonIndex + 1);

        return typeName.ToLowerInvariant() switch
        {
            "boolean" => new BooleanDataObject(),
            "int" or "integer" => new IntegerDataObject(),
            "long" => new LongDataObject(),
            "double" or "float" => new DoubleDataObject(),
            "datetime" or "date" => new DateDataObject(),
            _ => new StringDataObject()
        };
    }

    public static void SetDataObjectValue(ValuedDataObject dataObject, string? valueText)
    {
        if (string.IsNullOrEmpty(valueText)) return;

        switch (dataObject)
        {
            case BooleanDataObject:
                dataObject.Value = bool.TryParse(valueText, out var boolVal) ? boolVal : (object)valueText;
                break;
            case IntegerDataObject:
                dataObject.Value = int.TryParse(valueText, out var intVal) ? intVal : (object)valueText;
                break;
            case LongDataObject:
                dataObject.Value = long.TryParse(valueText, out var longVal) ? longVal : (object)valueText;
                break;
            case DoubleDataObject:
                dataObject.Value = double.TryParse(valueText, out var doubleVal) ? doubleVal : (object)valueText;
                break;
            case DateDataObject:
                dataObject.Value = DateTime.TryParse(valueText, out var dateVal) ? dateVal : (object)valueText;
                break;
            default:
                dataObject.Value = valueText;
                break;
        }
    }
}

public partial class BooleanDataObject
{
    public bool? GetBooleanValue()
    {
        return Value is bool b ? b : null;
    }

    public void SetBooleanValue(bool? value)
    {
        Value = value;
    }
}

public partial class IntegerDataObject
{
    public int? GetIntegerValue()
    {
        return Value is int i ? i : null;
    }

    public void SetIntegerValue(int? value)
    {
        Value = value;
    }
}

public partial class LongDataObject
{
    public long? GetLongValue()
    {
        return Value is long l ? l : null;
    }

    public void SetLongValue(long? value)
    {
        Value = value;
    }
}

public partial class DoubleDataObject
{
    public double? GetDoubleValue()
    {
        return Value is double d ? d : null;
    }

    public void SetDoubleValue(double? value)
    {
        Value = value;
    }
}

public partial class DateDataObject
{
    public DateTime? GetDateValue()
    {
        return Value is DateTime dt ? dt : null;
    }

    public void SetDateValue(DateTime? value)
    {
        Value = value;
    }
}

public partial class StringDataObject
{
    public string? GetStringValue()
    {
        return Value?.ToString();
    }

    public void SetStringValue(string? value)
    {
        Value = value;
    }
}

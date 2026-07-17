using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class MessageFlow : BaseElement
{
    public string? Name { get; set; }
    public string? SourceRef { get; set; }
    public string? TargetRef { get; set; }
    public string? MessageRef { get; set; }

    public override BaseElement Clone()
    {
        return new MessageFlow
        {
            Id = Id,
            Name = Name,
            SourceRef = SourceRef,
            TargetRef = TargetRef,
            MessageRef = MessageRef
        };
    }
}

public partial class StringDataObject : ValuedDataObject
{
    public override string TypeName => "string";

    public override ValuedDataObject CopyValue()
    {
        return new StringDataObject();
    }

    public override BaseElement Clone()
    {
        var clone = new StringDataObject
        {
            Id = Id,
            Name = Name,
            Value = Value is string s ? s : Value?.ToString(),
            ItemSubjectRef = ItemSubjectRef
        };
        return clone;
    }
}

public partial class BooleanDataObject : ValuedDataObject
{
    public override string TypeName => "boolean";

    public override ValuedDataObject CopyValue()
    {
        return new BooleanDataObject();
    }

    public override BaseElement Clone()
    {
        var clone = new BooleanDataObject
        {
            Id = Id,
            Name = Name,
            Value = Value,
            ItemSubjectRef = ItemSubjectRef
        };
        return clone;
    }
}

public partial class IntegerDataObject : ValuedDataObject
{
    public override string TypeName => "int";

    public override ValuedDataObject CopyValue()
    {
        return new IntegerDataObject();
    }

    public override BaseElement Clone()
    {
        var clone = new IntegerDataObject
        {
            Id = Id,
            Name = Name,
            Value = Value is int ? Value : (Value != null ? int.TryParse(Value.ToString(), out var v) ? v : Value : Value),
            ItemSubjectRef = ItemSubjectRef
        };
        return clone;
    }
}

public partial class LongDataObject : ValuedDataObject
{
    public override string TypeName => "long";

    public override ValuedDataObject CopyValue()
    {
        return new LongDataObject();
    }

    public override BaseElement Clone()
    {
        var clone = new LongDataObject
        {
            Id = Id,
            Name = Name,
            Value = Value is long ? Value : (Value != null ? long.TryParse(Value.ToString(), out var v) ? v : Value : Value),
            ItemSubjectRef = ItemSubjectRef
        };
        return clone;
    }
}

public partial class DoubleDataObject : ValuedDataObject
{
    public override string TypeName => "double";

    public override ValuedDataObject CopyValue()
    {
        return new DoubleDataObject();
    }

    public override BaseElement Clone()
    {
        var clone = new DoubleDataObject
        {
            Id = Id,
            Name = Name,
            Value = Value is double ? Value : (Value != null ? double.TryParse(Value.ToString(), out var v) ? v : Value : Value),
            ItemSubjectRef = ItemSubjectRef
        };
        return clone;
    }
}

public partial class DateDataObject : ValuedDataObject
{
    public override string TypeName => "datetime";

    public override ValuedDataObject CopyValue()
    {
        return new DateDataObject();
    }

    public override BaseElement Clone()
    {
        var clone = new DateDataObject
        {
            Id = Id,
            Name = Name,
            Value = Value is DateTime ? Value : (Value != null ? DateTime.TryParse(Value.ToString(), out var v) ? v : Value : Value),
            ItemSubjectRef = ItemSubjectRef
        };
        return clone;
    }
}


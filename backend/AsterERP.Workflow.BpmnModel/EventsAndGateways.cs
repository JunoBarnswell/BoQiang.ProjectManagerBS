using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel;

public abstract class Event : FlowNode
{
}

public abstract class CatchEvent : Event
{
    public List<EventDefinition> EventDefinitions { get; set; } = new();
}

public abstract class ThrowEvent : Event
{
    public List<EventDefinition> EventDefinitions { get; set; } = new();
}

public enum EventGatewayType
{
    Exclusive,
    Parallel,
    EventBased
}

public class StartEvent : CatchEvent
{
    public bool IsInterrupting { get; set; } = true;
    public string? Initiator { get; set; }
    public string? FormKey { get; set; }
    public List<FormProperty> FormProperties { get; set; } = new();

    public override BaseElement Clone()
    {
        return new StartEvent
        {
            Id = Id,
            Name = Name,
            IsInterrupting = IsInterrupting,
            Initiator = Initiator,
            FormKey = FormKey
        };
    }
}

public class EndEvent : ThrowEvent
{
    public override BaseElement Clone()
    {
        return new EndEvent
        {
            Id = Id,
            Name = Name
        };
    }
}

public class BoundaryEvent : CatchEvent
{
    public bool CancelActivity { get; set; } = true;
    public string? AttachedToRefId { get; set; }
    public FlowElement? AttachedToRef { get; set; }

    public override BaseElement Clone()
    {
        return new BoundaryEvent
        {
            Id = Id,
            Name = Name,
            CancelActivity = CancelActivity,
            AttachedToRefId = AttachedToRefId
        };
    }
}

public class IntermediateCatchEvent : CatchEvent
{
    public override BaseElement Clone()
    {
        return new IntermediateCatchEvent
        {
            Id = Id,
            Name = Name
        };
    }
}

public class IntermediateThrowEvent : ThrowEvent
{
    public override BaseElement Clone()
    {
        return new IntermediateThrowEvent
        {
            Id = Id,
            Name = Name
        };
    }
}

public class EventGateway : Gateway
{
    public EventGatewayType EventGatewayType { get; set; } = EventGatewayType.Exclusive;
    public bool Instantiate { get; set; }

    public override BaseElement Clone()
    {
        return new EventGateway
        {
            Id = Id,
            Name = Name,
            EventGatewayType = EventGatewayType,
            Instantiate = Instantiate
        };
    }
}

public abstract class Gateway : FlowNode
{
}

public class ExclusiveGateway : Gateway
{
    public string? DefaultFlow { get; set; }

    public override BaseElement Clone()
    {
        return new ExclusiveGateway
        {
            Id = Id,
            Name = Name,
            DefaultFlow = DefaultFlow
        };
    }
}

public class ParallelGateway : Gateway
{
    public override BaseElement Clone()
    {
        return new ParallelGateway
        {
            Id = Id,
            Name = Name
        };
    }
}

public class InclusiveGateway : Gateway
{
    public string? DefaultFlow { get; set; }

    public override BaseElement Clone()
    {
        return new InclusiveGateway
        {
            Id = Id,
            Name = Name,
            DefaultFlow = DefaultFlow
        };
    }
}

public abstract class EventDefinition : BaseElement
{
}

public class TimerEventDefinition : EventDefinition
{
    public string? TimeDate { get; set; }
    public string? TimeCycle { get; set; }
    public string? TimeDuration { get; set; }
    public string? EndDate { get; set; }
    public string? CalendarName { get; set; }

    public override BaseElement Clone()
    {
        return new TimerEventDefinition
        {
            Id = Id,
            TimeDate = TimeDate,
            TimeCycle = TimeCycle,
            TimeDuration = TimeDuration,
            EndDate = EndDate,
            CalendarName = CalendarName
        };
    }
}

public class SignalEventDefinition : EventDefinition
{
    public string? SignalRef { get; set; }
    public string? Scope { get; set; }

    public override BaseElement Clone()
    {
        return new SignalEventDefinition
        {
            Id = Id,
            SignalRef = SignalRef,
            Scope = Scope
        };
    }
}

public class MessageEventDefinition : EventDefinition
{
    public string? MessageRef { get; set; }
    public string? MessageEventDefinitionType { get; set; }
    public string? MessageEventType { get; set; }

    public override BaseElement Clone()
    {
        return new MessageEventDefinition
        {
            Id = Id,
            MessageRef = MessageRef,
            MessageEventDefinitionType = MessageEventDefinitionType,
            MessageEventType = MessageEventType
        };
    }
}

public class Signal : BaseElement
{
    public string? Name { get; set; }
    public string? Scope { get; set; }

    public override BaseElement Clone()
    {
        return new Signal { Id = Id, Name = Name, Scope = Scope };
    }
}

public class Message : BaseElement
{
    public string? Name { get; set; }
    public string? ItemRef { get; set; }

    public override BaseElement Clone()
    {
        return new Message { Id = Id, Name = Name, ItemRef = ItemRef };
    }
}

public class FormProperty : BaseElement
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public bool? Required { get; set; }
    public bool? Readable { get; set; }
    public bool? Writable { get; set; }
    public string? Variable { get; set; }
    public string? Expression { get; set; }
    public string? DatePattern { get; set; }
    public string? DefaultExpression { get; set; }
    public List<FormValue> Values { get; set; } = new();

    public override BaseElement Clone()
    {
        var clone = new FormProperty
        {
            Id = Id,
            Name = Name,
            Type = Type,
            Required = Required,
            Readable = Readable,
            Writable = Writable,
            Variable = Variable,
            Expression = Expression,
            DatePattern = DatePattern,
            DefaultExpression = DefaultExpression
        };
        clone.Values.AddRange(Values.Select(v => (FormValue)v.Clone()));
        return clone;
    }
}

public class FormValue : BaseElement
{
    public new string? Id { get; set; }
    public string? Name { get; set; }

    public override BaseElement Clone()
    {
        return new FormValue { Id = Id, Name = Name };
    }
}

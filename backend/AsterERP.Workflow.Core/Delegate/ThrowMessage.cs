using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Delegate;

public class ThrowMessage
{
    public string Name { get; }
    public Dictionary<string, object?>? Payload { get; }
    public string? BusinessKey { get; }
    public string? CorrelationKey { get; }

    public ThrowMessage(string name)
    {
        Name = name;
    }

    private ThrowMessage(ThrowMessageBuilder builder)
    {
        Name = builder.Name;
        Payload = builder.Payload;
        BusinessKey = builder.BusinessKey;
        CorrelationKey = builder.CorrelationKey;
    }

    public static ThrowMessageBuilder Builder(string name)
    {
        return new ThrowMessageBuilder(name);
    }

    public class ThrowMessageBuilder
    {
        public string Name { get; }
        public Dictionary<string, object?>? Payload { get; private set; }
        public string? BusinessKey { get; private set; }
        public string? CorrelationKey { get; private set; }

        public ThrowMessageBuilder(string name)
        {
            Name = name;
        }

        public ThrowMessageBuilder WithPayload(Dictionary<string, object?>? payload)
        {
            Payload = payload;
            return this;
        }

        public ThrowMessageBuilder WithBusinessKey(string? businessKey)
        {
            BusinessKey = businessKey;
            return this;
        }

        public ThrowMessageBuilder WithCorrelationKey(string? correlationKey)
        {
            CorrelationKey = correlationKey;
            return this;
        }

        public ThrowMessage Build()
        {
            return new ThrowMessage(this);
        }
    }
}


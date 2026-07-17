using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Delegate;

public class BpmnMessagePayloadMappingProvider : IMessagePayloadMappingProvider
{
    private readonly List<FieldDeclaration> _fieldDeclarations;

    public BpmnMessagePayloadMappingProvider(List<FieldDeclaration> fieldDeclarations)
    {
        _fieldDeclarations = fieldDeclarations;
    }

    public Task<Dictionary<string, object?>?> GetMessagePayloadAsync(IDelegateExecution execution, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>();

        foreach (var field in _fieldDeclarations)
        {
            var value = field.Value;
            if (value is IExpression expression)
            {
                value = expression.GetValue(execution);
            }
            payload[field.Name] = value;
        }

        if (payload.Count == 0)
            return Task.FromResult<Dictionary<string, object?>?>(null);

        return Task.FromResult<Dictionary<string, object?>?>(payload);
    }
}


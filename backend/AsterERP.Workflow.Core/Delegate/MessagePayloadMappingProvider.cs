using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Delegate;

public class MessagePayloadMappingProvider : IMessagePayloadMappingProvider
{
    private readonly BpmnMessagePayloadMappingProvider _provider;

    public MessagePayloadMappingProvider(List<FieldDeclaration> fieldDeclarations)
    {
        _provider = new BpmnMessagePayloadMappingProvider(fieldDeclarations);
    }

    public Task<Dictionary<string, object?>?> GetMessagePayloadAsync(IDelegateExecution execution, CancellationToken cancellationToken = default)
    {
        return _provider.GetMessagePayloadAsync(execution, cancellationToken);
    }
}


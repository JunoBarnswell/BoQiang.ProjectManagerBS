using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Delegate;

public class MessagePayloadMappingProviderFactory : IMessagePayloadMappingProviderFactory
{
    private readonly Dictionary<string, List<FieldDeclaration>> _fieldDeclarationByMessageDefinition;

    public MessagePayloadMappingProviderFactory(Dictionary<string, List<FieldDeclaration>>? fieldDeclarationByMessageDefinition = null)
    {
        _fieldDeclarationByMessageDefinition = fieldDeclarationByMessageDefinition ?? new Dictionary<string, List<FieldDeclaration>>();
    }

    public IMessagePayloadMappingProvider Create(string messageEventDefinitionId)
    {
        if (messageEventDefinitionId is null) throw new ArgumentNullException(nameof(messageEventDefinitionId));

        if (_fieldDeclarationByMessageDefinition.TryGetValue(messageEventDefinitionId, out var fieldDeclarations))
        {
            return new MessagePayloadMappingProvider(fieldDeclarations);
        }

        return new MessagePayloadMappingProvider(new List<FieldDeclaration>());
    }
}


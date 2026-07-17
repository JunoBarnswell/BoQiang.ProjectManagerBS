using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Delegate;

public class BpmnMessagePayloadMappingProviderFactory : MessagePayloadMappingProviderFactory
{
    public BpmnMessagePayloadMappingProviderFactory(Dictionary<string, List<FieldDeclaration>>? fieldDeclarationByMessageDefinition = null)
        : base(fieldDeclarationByMessageDefinition)
    {
    }
}


using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Delegate;

public interface IMessagePayloadMappingProvider
{
    Task<Dictionary<string, object?>?> GetMessagePayloadAsync(IDelegateExecution execution, CancellationToken cancellationToken = default);
}


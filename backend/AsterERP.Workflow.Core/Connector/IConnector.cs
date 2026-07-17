using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Connector;

public interface IConnector
{
    string Name { get; }
    string? Description { get; }
    global::System.Threading.Tasks.Task<ConnectorResponse> ExecuteAsync(ConnectorRequest request, CancellationToken cancellationToken = default);
}

public class ConnectorRequest
{
    public string Action { get; set; } = null!;
    public Dictionary<string, object?> Parameters { get; set; } = new();
    public Dictionary<string, string> Headers { get; set; } = new();
}

public class ConnectorResponse
{
    public bool IsSuccess { get; set; }
    public int StatusCode { get; set; }
    public Dictionary<string, object?> Data { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string> ResponseHeaders { get; set; } = new();
}

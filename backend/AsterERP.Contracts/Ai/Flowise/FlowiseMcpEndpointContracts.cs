using System.Text.Json;

namespace AsterERP.Contracts.Ai.Flowise;

public sealed class FlowiseMcpJsonRpcRequest
{
    public string Jsonrpc { get; set; } = "2.0";

    public JsonElement? Id { get; set; }

    public string Method { get; set; } = string.Empty;

    public JsonElement? Params { get; set; }
}

public sealed class FlowiseMcpJsonRpcResponse
{
    public string Jsonrpc { get; set; } = "2.0";

    public JsonElement? Id { get; set; }

    public object? Result { get; set; }

    public FlowiseMcpJsonRpcError? Error { get; set; }
}

public sealed class FlowiseMcpJsonRpcError
{
    public int Code { get; set; }

    public string Message { get; set; } = string.Empty;
}

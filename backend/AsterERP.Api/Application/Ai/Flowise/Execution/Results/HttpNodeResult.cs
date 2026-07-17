namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed class HttpNodeResult
{
    public int ExecutionIndex { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string NodeLabel { get; set; } = string.Empty;

    public string Method { get; set; } = "GET";

    public string Url { get; set; } = string.Empty;

    public string BodyType { get; set; } = "json";

    public string Body { get; set; } = string.Empty;

    public string ResponseType { get; set; } = "json";

    public int Status { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public IReadOnlyDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

    public object? Data { get; set; } = string.Empty;
}

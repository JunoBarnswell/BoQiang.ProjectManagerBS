namespace AsterERP.Api.Infrastructure.Messaging;

public sealed record AsterErpMessageSendResult(
    bool Succeeded,
    string Provider,
    string TraceId,
    string? Message,
    string? ErrorMessage)
{
    public static AsterErpMessageSendResult Success(string provider, string traceId, string? message = null) =>
        new(true, provider, traceId, message, null);

    public static AsterErpMessageSendResult Failed(string provider, string traceId, string errorMessage) =>
        new(false, provider, traceId, null, errorMessage);
}

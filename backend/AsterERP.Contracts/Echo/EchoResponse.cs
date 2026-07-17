namespace AsterERP.Contracts.Echo;

public sealed record EchoResponse(string Message, string ServerMessage, DateTimeOffset ReceivedAt);

namespace AsterERP.Api.Application.Ai.Tools;

public sealed record AiKernelFunctionGeneratedEvent(
    string EventName,
    string Summary,
    object? Payload);

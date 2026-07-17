using AsterERP.Contracts.Echo;

namespace AsterERP.Api.Application.Echo;

public sealed class EchoService
{
    public EchoResponse CreateResponse(string message)
    {
        return new EchoResponse(
            Message: message,
            ServerMessage: $"后端已收到前端消息：{message}",
            ReceivedAt: DateTimeOffset.UtcNow);
    }
}

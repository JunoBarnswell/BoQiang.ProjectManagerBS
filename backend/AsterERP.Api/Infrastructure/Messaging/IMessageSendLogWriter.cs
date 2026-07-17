namespace AsterERP.Api.Infrastructure.Messaging;

public interface IMessageSendLogWriter
{
    Task WriteAsync(MessageSendLogWriteRequest request, CancellationToken cancellationToken = default);
}

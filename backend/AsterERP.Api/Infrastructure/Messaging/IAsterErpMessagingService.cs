namespace AsterERP.Api.Infrastructure.Messaging;

public interface IAsterErpMessagingService
{
    Task<AsterErpMessageSendResult> SendEmailAsync(
        AsterErpEmailMessage message,
        CancellationToken cancellationToken = default);

    Task<AsterErpMessageSendResult> SendSmsAsync(
        AsterErpSmsMessage message,
        CancellationToken cancellationToken = default);
}

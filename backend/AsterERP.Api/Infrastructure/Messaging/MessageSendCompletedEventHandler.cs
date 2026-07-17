using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;

namespace AsterERP.Api.Infrastructure.Messaging;

public sealed class MessageSendCompletedEventHandler(
    IMessageSendLogWriter logWriter) :
    ILocalEventHandler<MessageSendCompletedEvent>,
    ITransientDependency
{
    public Task HandleEventAsync(MessageSendCompletedEvent eventData) =>
        logWriter.WriteAsync(eventData.Request);
}

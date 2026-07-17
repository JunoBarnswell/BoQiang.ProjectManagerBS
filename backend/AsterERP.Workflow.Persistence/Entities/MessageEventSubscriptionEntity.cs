using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_RU_EVENT_SUBSCR")]
public class MessageEventSubscriptionEntity : EventSubscriptionEntity
{
    public const string EventTypeMessage = "message";

    public MessageEventSubscriptionEntity()
    {
        EventType = EventTypeMessage;
    }
}

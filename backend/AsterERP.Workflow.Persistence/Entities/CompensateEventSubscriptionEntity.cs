using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_RU_EVENT_SUBSCR")]
public class CompensateEventSubscriptionEntity : EventSubscriptionEntity
{
    public const string EventTypeCompensate = "compensate";

    public CompensateEventSubscriptionEntity()
    {
        EventType = EventTypeCompensate;
    }
}

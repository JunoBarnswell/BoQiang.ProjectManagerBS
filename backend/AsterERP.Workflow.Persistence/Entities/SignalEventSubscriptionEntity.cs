using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

[SugarTable("ACT_RU_EVENT_SUBSCR")]
public class SignalEventSubscriptionEntity : EventSubscriptionEntity
{
    public const string EventTypeSignal = "signal";

    public SignalEventSubscriptionEntity()
    {
        EventType = EventTypeSignal;
    }

    public bool IsProcessInstanceScoped
    {
        get
        {
            var scope = ExtractScopeFromConfiguration();
            return scope == "processInstance";
        }
    }

    public bool IsGlobalScoped
    {
        get
        {
            var scope = ExtractScopeFromConfiguration();
            return scope == null || scope == "global";
        }
    }

    private string? ExtractScopeFromConfiguration()
    {
        if (Configuration == null) return null;
        if (Configuration.Contains("{\"scope\":"))
        {
            var start = Configuration.IndexOf("\"scope\":") + 8;
            var end = Configuration.LastIndexOf('"');
            if (start < end)
            {
                return Configuration[start..end].Trim('"');
            }
        }
        return Configuration;
    }
}

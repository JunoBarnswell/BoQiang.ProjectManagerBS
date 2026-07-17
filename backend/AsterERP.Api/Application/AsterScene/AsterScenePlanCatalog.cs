using AsterERP.Contracts.AsterScene;

namespace AsterERP.Api.Application.AsterScene;

public static class AsterScenePlanCatalog
{
    private static readonly IReadOnlyList<AsterSceneSubscriptionPlanDto> Plans =
    [
        new AsterSceneSubscriptionPlanDto
        {
            PlanCode = "free",
            PlanName = "Free",
            PriceMonthly = 0,
            StorageGb = 1,
            AiCreditsMonthly = 100,
            PublishedWorks = 3
        },
        new AsterSceneSubscriptionPlanDto
        {
            PlanCode = "creator",
            PlanName = "Creator",
            PriceMonthly = 19,
            StorageGb = 50,
            AiCreditsMonthly = 3000,
            PublishedWorks = 100
        },
        new AsterSceneSubscriptionPlanDto
        {
            PlanCode = "studio",
            PlanName = "Studio",
            PriceMonthly = 79,
            StorageGb = 250,
            AiCreditsMonthly = 18000,
            PublishedWorks = 1000
        }
    ];

    public static IReadOnlyList<AsterSceneSubscriptionPlanDto> GetPlans() => Plans;

    public static AsterSceneSubscriptionPlanDto GetPlan(string? planCode)
    {
        return Plans.FirstOrDefault(item => item.PlanCode.Equals(planCode, StringComparison.OrdinalIgnoreCase)) ??
               Plans[0];
    }
}

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskRecurrenceOptions
{
    public const string SectionName = "ProjectManagement:Recurrence";
    public int DefaultGenerationWindowDays { get; set; } = 30;
    public int MaximumGenerationWindowDays { get; set; } = 366;
    public int MaximumOccurrencesPerGeneration { get; set; } = 500;
}

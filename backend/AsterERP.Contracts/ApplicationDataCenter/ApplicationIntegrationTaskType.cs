namespace AsterERP.Contracts.ApplicationDataCenter;

public static class ApplicationIntegrationTaskType
{
    public const string DatabaseToDatabase = "DatabaseToDatabase";
    public const string DatabaseToApi = "DatabaseToApi";
    public const string ApiToDatabase = "ApiToDatabase";
    public const string FileImport = "FileImport";
    public const string QueueConsumer = "QueueConsumer";
    public const string WebhookReceiver = "WebhookReceiver";
    public const string Scheduled = "Scheduled";
    public const string EventTriggered = "EventTriggered";
}

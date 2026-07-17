namespace AsterERP.Api.Infrastructure.Messaging;

public sealed record AsterErpEmailMessage(
    string To,
    string Subject,
    string Body,
    bool IsBodyHtml = true);

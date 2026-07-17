namespace AsterERP.Api.Infrastructure.Messaging;

public sealed record AsterErpSmsMessage(
    string PhoneNumber,
    string Text);

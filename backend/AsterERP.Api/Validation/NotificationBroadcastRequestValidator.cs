using AsterERP.Contracts.System.Notifications;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class NotificationBroadcastRequestValidator : AbstractValidator<NotificationBroadcastRequest>
{
    public NotificationBroadcastRequestValidator()
    {
        RuleFor(x => x.EventName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(4000);
    }
}

using AsterERP.Contracts.System.Announcements;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class AnnouncementTopRequestValidator : AbstractValidator<AnnouncementTopRequest>
{
    public AnnouncementTopRequestValidator()
    {
        RuleFor(x => x.IsTop).NotNull();
    }
}

using AsterERP.Contracts.System.Announcements;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class AnnouncementUpsertRequestValidator : AbstractValidator<AnnouncementUpsertRequest>
{
    public AnnouncementUpsertRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.AnnouncementType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Scope).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0).LessThanOrEqualTo(999);
        RuleFor(x => x.Remark).MaximumLength(500);
    }
}

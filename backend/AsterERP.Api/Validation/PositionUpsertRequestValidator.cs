using AsterERP.Contracts.System.Organizations;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class PositionUpsertRequestValidator : AbstractValidator<PositionUpsertRequest>
{
    public PositionUpsertRequestValidator()
    {
        RuleFor(x => x.PositionCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PositionName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DeptId).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Status).NotEmpty().MaximumLength(32);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

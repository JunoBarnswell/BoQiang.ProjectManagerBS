using AsterERP.Contracts.System.Dicts;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class DictItemUpsertRequestValidator : AbstractValidator<DictItemUpsertRequest>
{
    public DictItemUpsertRequestValidator()
    {
        RuleFor(x => x.ItemLabel).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ItemValue).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

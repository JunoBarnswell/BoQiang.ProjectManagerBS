using AsterERP.Contracts.System.Dicts;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class DictTypeUpsertRequestValidator : AbstractValidator<DictTypeUpsertRequest>
{
    public DictTypeUpsertRequestValidator()
    {
        RuleFor(x => x.DictCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DictName).NotEmpty().MaximumLength(100);
    }
}

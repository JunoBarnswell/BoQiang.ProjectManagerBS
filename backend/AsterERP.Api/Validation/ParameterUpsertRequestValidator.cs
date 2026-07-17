using AsterERP.Contracts.System.Parameters;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class ParameterUpsertRequestValidator : AbstractValidator<ParameterUpsertRequest>
{
    public ParameterUpsertRequestValidator()
    {
        RuleFor(x => x.ParamName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.ParamKey)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[A-Za-z][A-Za-z0-9_.:-]*$")
            .WithMessage("参数键名只能以字母开头，并包含字母、数字、点、下划线、冒号或短横线");

        RuleFor(x => x.ParamValue)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.Category)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[A-Za-z][A-Za-z0-9_-]*$")
            .WithMessage("参数分类只能以字母开头，并包含字母、数字、下划线或短横线");

        RuleFor(x => x.Remark)
            .MaximumLength(500);
    }
}

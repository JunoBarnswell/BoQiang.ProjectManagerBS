using AsterERP.Contracts.System.Menus;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class MenuUpsertRequestValidator : AbstractValidator<MenuUpsertRequest>
{
    public MenuUpsertRequestValidator()
    {
        RuleFor(x => x.MenuName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MenuCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MenuType).NotEmpty().MaximumLength(32);
        RuleFor(x => x.PageCode)
            .MaximumLength(128)
            .Matches("^[A-Za-z][A-Za-z0-9_.:-]*$")
            .WithMessage("页面编码只能以字母开头，并包含字母、数字、点、下划线、冒号或短横线")
            .When(x => !string.IsNullOrWhiteSpace(x.PageCode));
        RuleFor(x => x.ArtifactId).MaximumLength(64).When(x => x.ArtifactId is not null);
        RuleFor(x => x.ScopeType).MaximumLength(32).When(x => x.ScopeType is not null);
        RuleFor(x => x.ConfigJson).MaximumLength(262144).When(x => x.ConfigJson is not null);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Remark).MaximumLength(500).When(x => x.Remark is not null);
    }
}

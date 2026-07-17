using AsterERP.Contracts.System.Organizations;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class DepartmentUpsertRequestValidator : AbstractValidator<DepartmentUpsertRequest>
{
    public DepartmentUpsertRequestValidator()
    {
        RuleFor(x => x.DeptCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DeptName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Status).NotEmpty().MaximumLength(32);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LeaderUserIds)
            .Must(value => value is null || value.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).Count() <= 3)
            .WithMessage("部门领导最多只能设置三位");
    }
}

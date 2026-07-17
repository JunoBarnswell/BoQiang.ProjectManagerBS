using AsterERP.Contracts.System.Roles;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class RoleUpsertRequestValidator : AbstractValidator<RoleUpsertRequest>
{
    public RoleUpsertRequestValidator()
    {
        RuleFor(x => x.RoleName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.RoleCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DataScope).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Remark).MaximumLength(500).When(x => x.Remark is not null);
    }
}

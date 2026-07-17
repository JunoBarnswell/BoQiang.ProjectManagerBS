using AsterERP.Contracts.System.Roles;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class RolePermissionUpdateRequestValidator : AbstractValidator<RolePermissionUpdateRequest>
{
    public RolePermissionUpdateRequestValidator()
    {
        RuleFor(x => x.PermissionCodes).NotNull();
        RuleForEach(x => x.PermissionCodes).NotEmpty();
    }
}

using AsterERP.Contracts.System.Users;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class UserRoleUpdateRequestValidator : AbstractValidator<UserRoleUpdateRequest>
{
    public UserRoleUpdateRequestValidator()
    {
        RuleFor(x => x.RoleIds).NotNull();
        RuleForEach(x => x.RoleIds).NotEmpty();
    }
}

using AsterERP.Contracts.System.Users;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class UserResetPasswordRequestValidator : AbstractValidator<UserResetPasswordRequest>
{
    public UserResetPasswordRequestValidator()
    {
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).MaximumLength(128);
    }
}

using AsterERP.Contracts.Auth;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class InitialAdminPasswordRecoveryRequestValidator : AbstractValidator<InitialAdminPasswordRecoveryRequest>
{
    public InitialAdminPasswordRecoveryRequestValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.RecoveryCode).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Password)
            .Must(password => !string.IsNullOrWhiteSpace(password) && password.Trim().Length >= 6)
            .WithMessage("密码至少需要 6 个字符")
            .MaximumLength(128);
    }
}

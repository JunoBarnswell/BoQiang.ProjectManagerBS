using AsterERP.Contracts.System.Users;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class UserUpsertRequestValidator : AbstractValidator<UserUpsertRequest>
{
    public UserUpsertRequestValidator()
    {
        RuleFor(x => x.UserName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Status).NotEmpty().MaximumLength(32);
        RuleFor(x => x.RoleIds).NotNull();
        RuleForEach(x => x.RoleIds).NotEmpty();
        RuleFor(x => x.Employments)
            .Must(value => value is null || value.Where(item => string.Equals(item.Status, "Enabled", StringComparison.OrdinalIgnoreCase)).Count(item => item.IsPrimary) <= 1)
            .WithMessage("用户只能有一条启用主任职");
        RuleFor(x => x.Remark).MaximumLength(500).When(x => x.Remark is not null);
    }
}

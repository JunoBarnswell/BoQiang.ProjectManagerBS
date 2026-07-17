using AsterERP.Contracts.Echo;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class EchoRequestValidator : AbstractValidator<EchoRequest>
{
    public EchoRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(2048);
    }
}

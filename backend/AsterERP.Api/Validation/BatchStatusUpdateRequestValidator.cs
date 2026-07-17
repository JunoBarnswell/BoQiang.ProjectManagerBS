using AsterERP.Contracts.System;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class BatchStatusUpdateRequestValidator : AbstractValidator<BatchStatusUpdateRequest>
{
    public BatchStatusUpdateRequestValidator()
    {
        RuleFor(x => x.Ids).NotNull().Must(ids => ids.Count > 0);
        RuleForEach(x => x.Ids).NotEmpty();
        RuleFor(x => x.Status).NotEmpty().MaximumLength(32);
    }
}

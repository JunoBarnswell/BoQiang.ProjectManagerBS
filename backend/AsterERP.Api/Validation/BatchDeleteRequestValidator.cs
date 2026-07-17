using AsterERP.Contracts.System;
using FluentValidation;

namespace AsterERP.Api.Validation;

public sealed class BatchDeleteRequestValidator : AbstractValidator<BatchDeleteRequest>
{
    public BatchDeleteRequestValidator()
    {
        RuleFor(x => x.Ids).NotNull();
        RuleFor(x => x.Ids).Must(ids => ids.Count > 0).WithMessage("至少需要一个ID");
        RuleForEach(x => x.Ids).NotEmpty();
    }
}

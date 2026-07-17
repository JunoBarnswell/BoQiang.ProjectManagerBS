using AsterERP.Shared;

namespace AsterERP.Shared.Exceptions;

public sealed class ValidationException : BusinessException
{
    public ValidationException(string message, int code = ErrorCodes.ParameterInvalid)
        : base(code, message)
    {
    }
}

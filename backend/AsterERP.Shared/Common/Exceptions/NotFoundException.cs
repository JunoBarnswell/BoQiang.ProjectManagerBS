namespace AsterERP.Shared.Exceptions;

public sealed class NotFoundException : BusinessException
{
    public NotFoundException(string message, int code) : base(code, message)
    {
    }
}

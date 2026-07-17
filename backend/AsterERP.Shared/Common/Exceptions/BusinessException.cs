namespace AsterERP.Shared.Exceptions;

public class BusinessException : Exception
{
    public BusinessException(int code, string message) : base(message)
    {
        Code = code;
    }

    public int Code { get; }
}

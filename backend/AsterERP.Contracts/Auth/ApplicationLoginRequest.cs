namespace AsterERP.Contracts.Auth;

public sealed record ApplicationLoginRequest(
    string UserName,
    string Password);

namespace AsterERP.Contracts.Auth;

/// <summary>
/// Allows a deployment-configured recovery code to restore the initial platform administrator.
/// </summary>
public sealed record InitialAdminPasswordRecoveryRequest(
    string UserName,
    string RecoveryCode,
    string Password);

namespace AsterERP.Contracts.Auth;

public sealed record BrandingResponse(
    string SystemName,
    string? LogoFileId,
    string? FaviconFileId,
    string PrimaryColor);

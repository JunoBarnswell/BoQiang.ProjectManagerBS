namespace AsterERP.Contracts.Im;

public sealed record ImUserSearchQuery(string? Keyword, int Take = 20);

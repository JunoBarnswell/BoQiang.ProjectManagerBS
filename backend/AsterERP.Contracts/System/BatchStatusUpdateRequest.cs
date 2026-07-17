namespace AsterERP.Contracts.System;

public sealed record BatchStatusUpdateRequest(
    IReadOnlyList<string> Ids,
    string Status);

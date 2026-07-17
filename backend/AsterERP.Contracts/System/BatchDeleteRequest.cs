namespace AsterERP.Contracts.System;

public sealed record BatchDeleteRequest(IReadOnlyList<string> Ids);

namespace AsterERP.Contracts.Im;

public sealed record ImDirectoryResponse(
    IReadOnlyList<ImDirectoryDepartmentNodeResponse> Departments);

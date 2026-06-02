namespace JobNet.Infrastructure.Contracts;

public record CompanyDto(
    Guid Id,
    Guid OwnerId,
    string Name,
    string? Industry,
    string? BusinessNumber,
    string? Website,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? Province,
    int? FoundedYear,
    string? EmployeeCount,
    string? Description,
    double Rating,
    int ReviewCount,
    bool Verified,
    DateTime CreatedAt
);

public record UpdateCompanyRequest(
    string Name,
    string? Industry,
    string? BusinessNumber,
    string? Website,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? Province,
    int? FoundedYear,
    string? EmployeeCount,
    string? Description
);

namespace JobNet.Infrastructure.Contracts;

public record CertificationDto(string Name, string? Issuer, int Year);
public record ExperienceDto(string Title, string Company, string From, string To);

public record WorkerProfileDto(
    Guid UserId,
    string? Headline,
    string? Bio,
    int YearsExperience,
    decimal HourlyRate,
    string Availability,
    double Rating,
    int ReviewCount,
    IReadOnlyList<string> Skills,
    IReadOnlyList<CertificationDto> Certifications,
    IReadOnlyList<ExperienceDto> Experiences
);

public record UpsertWorkerProfileRequest(
    string? Headline,
    string? Bio,
    int YearsExperience,
    decimal HourlyRate,
    string Availability,
    IReadOnlyList<string> Skills,
    IReadOnlyList<CertificationDto> Certifications,
    IReadOnlyList<ExperienceDto> Experiences,
    string? Phone,
    string? City,
    string? Province,
    string? FirstName,
    string? LastName
);

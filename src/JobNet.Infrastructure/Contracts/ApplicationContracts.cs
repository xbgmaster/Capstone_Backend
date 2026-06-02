using JobNet.Domain.Enums;

namespace JobNet.Infrastructure.Contracts;

public record ApplicationDto(
    Guid Id,
    Guid JobId,
    string JobTitle,
    Guid? CompanyId,
    string? CompanyName,
    Guid WorkerId,
    string WorkerFirstName,
    string WorkerLastName,
    string? WorkerHeadline,
    double WorkerRating,
    string CoverLetter,
    decimal ExpectedRate,
    ApplicationStatus Status,
    DateTime SubmittedAt
);

public record CreateApplicationRequest(
    Guid JobId,
    string CoverLetter,
    decimal ExpectedRate
);

public record ChangeApplicationStatusRequest(ApplicationStatus Status);

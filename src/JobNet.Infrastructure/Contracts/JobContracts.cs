using JobNet.Domain.Enums;

namespace JobNet.Infrastructure.Contracts;

public record JobDto(
    Guid Id,
    Guid CompanyId,
    string? CompanyName,
    string Title,
    string Category,
    string Description,
    string? Activity,
    string Location,
    DateTime DueDate,
    PaymentType PaymentType,
    decimal PaymentAmount,
    string Currency,
    JobStatus Status,
    DateTime PostedAt,
    IReadOnlyList<string> SkillsRequired,
    int ApplicationCount
);

public record CreateJobRequest(
    string Title,
    string Category,
    string Description,
    string? Activity,
    string Location,
    DateTime DueDate,
    PaymentType PaymentType,
    decimal PaymentAmount,
    IReadOnlyList<string> SkillsRequired
);

public record UpdateJobRequest(
    string Title,
    string Category,
    string Description,
    string? Activity,
    string Location,
    DateTime DueDate,
    PaymentType PaymentType,
    decimal PaymentAmount,
    IReadOnlyList<string> SkillsRequired
);

public record ChangeJobStatusRequest(JobStatus Status);

public record JobFilter(
    string? Query,
    string? Category,
    string? Province,
    bool? OnlyOpen,
    Guid? CompanyId,
    int Page = 1,
    int PageSize = 50
);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

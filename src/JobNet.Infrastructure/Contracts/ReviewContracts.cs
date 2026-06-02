namespace JobNet.Infrastructure.Contracts;

public record ReviewDto(
    Guid Id,
    Guid FromUserId,
    string FromUserName,
    Guid? ToUserId,
    Guid? ToCompanyId,
    Guid? JobId,
    string? JobTitle,
    int Rating,
    string Comment,
    DateTime CreatedAt
);

public record CreateReviewRequest(
    Guid? ToUserId,
    Guid? ToCompanyId,
    Guid? JobId,
    int Rating,
    string Comment
);

using JobNet.Domain.Enums;

namespace JobNet.Infrastructure.Contracts;

public record NotificationDto(
    Guid Id,
    Guid UserId,
    NotificationType Type,
    string Title,
    string Message,
    string? Link,
    bool Read,
    DateTime CreatedAt
);

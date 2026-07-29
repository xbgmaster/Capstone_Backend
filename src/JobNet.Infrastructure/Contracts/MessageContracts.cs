namespace JobNet.Infrastructure.Contracts;

public record MessageDto(
    Guid Id,
    Guid SenderId,
    string SenderName,
    Guid RecipientId,
    string RecipientName,
    Guid? JobId,
    string? JobTitle,
    string Body,
    bool Read,
    DateTime CreatedAt
);

public record SendMessageRequest(
    Guid RecipientId,
    Guid? JobId,
    string Body
);

/// <summary>One row per conversation partner (for an inbox / threads list).</summary>
public record ConversationSummaryDto(
    Guid OtherUserId,
    string OtherUserName,
    Guid? JobId,
    string? JobTitle,
    string LastMessage,
    DateTime LastMessageAt,
    int UnreadCount
);

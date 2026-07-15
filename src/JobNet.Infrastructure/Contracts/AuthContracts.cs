using System.ComponentModel.DataAnnotations;
using JobNet.Domain.Enums;

namespace JobNet.Infrastructure.Contracts;

public class ResetPasswordDto
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, MinLength(6), MaxLength(100)]
    public string NewPassword { get; set; } = string.Empty;
}

public record RegisterRequest(
    UserRole Role,
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? Phone,
    string? City,
    string? Province,
    string? CompanyName,     // employer only
    string? Industry,        // employer only
    string? Headline         // worker only
);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

public record UserDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    UserStatus Status,
    string? Phone,
    string? City,
    string? Province,
    string Avatar,
    Guid? CompanyId,
    DateTime CreatedAt
);

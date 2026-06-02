using JobNet.Domain.Enums;

namespace JobNet.Infrastructure.Auth;

public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    UserRole? Role { get; }
    Guid? CompanyId { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    bool IsAuthenticated { get; }
}

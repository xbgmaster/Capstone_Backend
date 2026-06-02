using JobNet.Domain.Entities;

namespace JobNet.Infrastructure.Auth;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) IssueToken(User user);
}

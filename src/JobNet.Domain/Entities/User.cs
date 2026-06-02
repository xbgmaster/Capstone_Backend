using JobNet.Domain.Enums;

namespace JobNet.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public UserRole Role { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string Avatar { get; set; } = "??";
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }

    public WorkerProfile? WorkerProfile { get; set; }
}

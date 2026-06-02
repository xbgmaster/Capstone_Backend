using JobNet.Domain.Enums;

namespace JobNet.Domain.Entities;

public class Application
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public Job? Job { get; set; }

    public Guid WorkerId { get; set; }
    public User? Worker { get; set; }

    public string CoverLetter { get; set; } = string.Empty;
    public decimal ExpectedRate { get; set; }
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

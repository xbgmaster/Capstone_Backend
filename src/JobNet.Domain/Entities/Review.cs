namespace JobNet.Domain.Entities;

public class Review
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FromUserId { get; set; }
    public User? FromUser { get; set; }

    // A review is *either* about a user (worker) or about a company.
    public Guid? ToUserId { get; set; }
    public User? ToUser { get; set; }

    public Guid? ToCompanyId { get; set; }
    public Company? ToCompany { get; set; }

    public Guid? JobId { get; set; }
    public Job? Job { get; set; }

    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

namespace JobNet.Domain.Entities;

public class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public User? Owner { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? BusinessNumber { get; set; }
    public string? Website { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public int? FoundedYear { get; set; }
    public string? EmployeeCount { get; set; }
    public string? Description { get; set; }

    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public bool Verified { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}

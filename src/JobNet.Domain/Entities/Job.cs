using JobNet.Domain.Enums;

namespace JobNet.Domain.Entities;

public class Job
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Activity { get; set; }
    public string Location { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }

    public PaymentType PaymentType { get; set; } = PaymentType.Hourly;
    public decimal PaymentAmount { get; set; }
    public string Currency { get; set; } = "CAD";

    public JobStatus Status { get; set; } = JobStatus.Open;
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;

    public ICollection<JobSkill> SkillsRequired { get; set; } = new List<JobSkill>();
    public ICollection<Application> Applications { get; set; } = new List<Application>();
}

public class JobSkill
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public Job? Job { get; set; }
    public string Name { get; set; } = string.Empty;
}

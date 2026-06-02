using JobNet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobNet.Infrastructure.Persistence;

public class JobNetDbContext : DbContext
{
    public JobNetDbContext(DbContextOptions<JobNetDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<WorkerProfile> WorkerProfiles => Set<WorkerProfile>();
    public DbSet<WorkerSkill> WorkerSkills => Set<WorkerSkill>();
    public DbSet<Certification> Certifications => Set<Certification>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobSkill> JobSkills => Set<JobSkill>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.ApplyConfigurationsFromAssembly(typeof(JobNetDbContext).Assembly);
    }
}

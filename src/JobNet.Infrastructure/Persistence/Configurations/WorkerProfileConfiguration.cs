using JobNet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobNet.Infrastructure.Persistence.Configurations;

public class WorkerProfileConfiguration : IEntityTypeConfiguration<WorkerProfile>
{
    public void Configure(EntityTypeBuilder<WorkerProfile> b)
    {
        b.ToTable("WorkerProfiles");
        b.HasKey(p => p.UserId);
        b.Property(p => p.Headline).HasMaxLength(200);
        b.Property(p => p.Bio).HasMaxLength(2000);
        b.Property(p => p.Availability).HasMaxLength(40);
        b.Property(p => p.HourlyRate).HasPrecision(10, 2);

        b.HasMany(p => p.Skills)
            .WithOne(s => s.Worker!)
            .HasForeignKey(s => s.WorkerId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(p => p.Certifications)
            .WithOne(c => c.Worker!)
            .HasForeignKey(c => c.WorkerId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(p => p.Experiences)
            .WithOne(e => e.Worker!)
            .HasForeignKey(e => e.WorkerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkerSkillConfiguration : IEntityTypeConfiguration<WorkerSkill>
{
    public void Configure(EntityTypeBuilder<WorkerSkill> b)
    {
        b.ToTable("WorkerSkills");
        b.HasKey(s => s.Id);
        b.Property(s => s.Name).HasMaxLength(80).IsRequired();
        b.HasIndex(s => new { s.WorkerId, s.Name }).IsUnique();
    }
}

public class CertificationConfiguration : IEntityTypeConfiguration<Certification>
{
    public void Configure(EntityTypeBuilder<Certification> b)
    {
        b.ToTable("Certifications");
        b.HasKey(c => c.Id);
        b.Property(c => c.Name).HasMaxLength(200).IsRequired();
        b.Property(c => c.Issuer).HasMaxLength(200);
    }
}

public class ExperienceConfiguration : IEntityTypeConfiguration<Experience>
{
    public void Configure(EntityTypeBuilder<Experience> b)
    {
        b.ToTable("Experiences");
        b.HasKey(e => e.Id);
        b.Property(e => e.Title).HasMaxLength(200).IsRequired();
        b.Property(e => e.Company).HasMaxLength(200).IsRequired();
        b.Property(e => e.From).HasMaxLength(20);
        b.Property(e => e.To).HasMaxLength(20);
    }
}

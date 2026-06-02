using JobNet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobNet.Infrastructure.Persistence.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> b)
    {
        b.ToTable("Jobs");
        b.HasKey(j => j.Id);
        b.Property(j => j.Title).HasMaxLength(200).IsRequired();
        b.Property(j => j.Category).HasMaxLength(80).IsRequired();
        b.Property(j => j.Description).HasMaxLength(5000).IsRequired();
        b.Property(j => j.Activity).HasMaxLength(2000);
        b.Property(j => j.Location).HasMaxLength(200).IsRequired();
        b.Property(j => j.Currency).HasMaxLength(8);
        b.Property(j => j.PaymentAmount).HasPrecision(12, 2);
        b.Property(j => j.PaymentType).HasConversion<int>();
        b.Property(j => j.Status).HasConversion<int>();
        b.HasIndex(j => j.Status);
        b.HasIndex(j => j.PostedAt);

        b.HasMany(j => j.SkillsRequired)
            .WithOne(s => s.Job!)
            .HasForeignKey(s => s.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(j => j.Applications)
            .WithOne(a => a.Job!)
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class JobSkillConfiguration : IEntityTypeConfiguration<JobSkill>
{
    public void Configure(EntityTypeBuilder<JobSkill> b)
    {
        b.ToTable("JobSkills");
        b.HasKey(s => s.Id);
        b.Property(s => s.Name).HasMaxLength(80).IsRequired();
    }
}

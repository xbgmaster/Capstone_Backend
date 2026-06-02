using JobNet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobNet.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> b)
    {
        b.ToTable("Companies");
        b.HasKey(c => c.Id);
        b.Property(c => c.Name).HasMaxLength(200).IsRequired();
        b.Property(c => c.Industry).HasMaxLength(100);
        b.Property(c => c.BusinessNumber).HasMaxLength(40);
        b.Property(c => c.Website).HasMaxLength(300);
        b.Property(c => c.Email).HasMaxLength(256);
        b.Property(c => c.Phone).HasMaxLength(40);
        b.Property(c => c.Address).HasMaxLength(300);
        b.Property(c => c.City).HasMaxLength(100);
        b.Property(c => c.Province).HasMaxLength(8);
        b.Property(c => c.EmployeeCount).HasMaxLength(40);
        b.Property(c => c.Description).HasMaxLength(2000);

        b.HasOne(c => c.Owner)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(c => c.Jobs)
            .WithOne(j => j.Company!)
            .HasForeignKey(j => j.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

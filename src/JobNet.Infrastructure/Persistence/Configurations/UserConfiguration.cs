using JobNet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobNet.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.HasKey(u => u.Id);
        b.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        b.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        b.Property(u => u.Email).HasMaxLength(256).IsRequired();
        b.HasIndex(u => u.Email).IsUnique();
        b.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
        b.Property(u => u.Phone).HasMaxLength(40);
        b.Property(u => u.City).HasMaxLength(100);
        b.Property(u => u.Province).HasMaxLength(8);
        b.Property(u => u.Avatar).HasMaxLength(4);
        b.Property(u => u.Role).HasConversion<int>();
        b.Property(u => u.Status).HasConversion<int>();
        b.Property(u => u.PasswordResetTokenHash).HasMaxLength(200);

        b.HasOne(u => u.Company)
            .WithMany()
            .HasForeignKey(u => u.CompanyId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(u => u.WorkerProfile)
            .WithOne(p => p.User!)
            .HasForeignKey<WorkerProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

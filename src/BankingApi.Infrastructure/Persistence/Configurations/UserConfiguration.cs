using BankingApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankingApi.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
               .HasColumnType("char(36)")
               .ValueGeneratedNever();

        builder.Property(u => u.FirstName)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(u => u.MiddleName)
               .HasMaxLength(100)
               .IsRequired(false);

        builder.Property(u => u.LastName)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(u => u.Gender)
               .IsRequired()
               .HasMaxLength(10);

        builder.Property(u => u.Address)
               .HasMaxLength(255)
               .IsRequired(false);

        builder.Property(u => u.State)
               .HasMaxLength(100)
               .IsRequired(false);

        builder.Property(u => u.Country)
               .IsRequired()
               .HasMaxLength(100)
               .HasDefaultValue("Nigeria");

        builder.Property(u => u.Email)
               .IsRequired()
               .HasMaxLength(150);

        builder.HasIndex(u => u.Email)
               .IsUnique();

        builder.Property(u => u.Password)
               .IsRequired()
               .HasMaxLength(255);

        builder.Property(u => u.CreatedAt)
               .IsRequired()
               .HasColumnType("datetime")
               .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // FullName is a computed C# property — never mapped
        builder.Ignore(u => u.FullName);

        // Navigation
        builder.HasOne(u => u.Account)
               .WithOne(a => a.User)
               .HasForeignKey<Account>(a => a.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
using BankingApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankingApi.Infrastructure.Persistence.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
               .HasColumnType("char(36)")
               .ValueGeneratedNever();

        builder.Property(a => a.UserId)
               .IsRequired()
               .HasColumnType("char(36)");

        builder.HasIndex(a => a.UserId)
               .IsUnique();

        builder.Property(a => a.AccountNumber)
               .IsRequired()
               .HasMaxLength(10);

        builder.HasIndex(a => a.AccountNumber)
               .IsUnique();

        builder.Property(a => a.BVN)
               .HasMaxLength(11)
               .IsRequired(false);

        builder.HasIndex(a => a.BVN)
               .IsUnique()
               .HasFilter("`BVN` IS NOT NULL");  

        builder.Property(a => a.Balance)
               .IsRequired()
               .HasColumnType("decimal(18,2)")
               .HasDefaultValue(0.00m);

        builder.Property(a => a.Currency)
               .IsRequired()
               .HasMaxLength(10)
               .HasDefaultValue("NGN");

        builder.Property(a => a.AccountType)
               .IsRequired()
               .HasMaxLength(20)
               .HasDefaultValue("Customer");

        builder.Property(a => a.NglPoolType)
               .HasMaxLength(20)
               .IsRequired(false);

        builder.Property(a => a.IsSystemAccount)
               .IsRequired()
               .HasColumnType("tinyint(1)")
               .HasDefaultValue(false);

        builder.Property(a => a.CreatedAt)
               .IsRequired()
               .HasColumnType("datetime")
               .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(a => a.UpdatedAt)
               .IsRequired()
               .HasColumnType("datetime");
    }
}
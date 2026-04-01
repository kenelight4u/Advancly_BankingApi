using BankingApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankingApi.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
               .HasColumnType("char(36)")
               .ValueGeneratedNever();

        builder.Property(t => t.Reference)
               .IsRequired()
               .HasMaxLength(50);

        builder.HasIndex(t => t.Reference);  

        builder.Property(t => t.SourceAccountNumber)
               .IsRequired()
               .HasMaxLength(10);

        builder.Property(t => t.DestAccountNumber)
               .IsRequired()
               .HasMaxLength(10);

        builder.Property(t => t.Amount)
               .IsRequired()
               .HasColumnType("decimal(18,2)");

        builder.Property(t => t.Fee)
               .IsRequired()
               .HasColumnType("decimal(18,2)")
               .HasDefaultValue(0.00m);

        builder.Property(t => t.TotalDebited)
               .IsRequired()
               .HasColumnType("decimal(18,2)");

        builder.Property(t => t.Narration)
               .HasMaxLength(255)
               .IsRequired(false);

        builder.Property(t => t.Status)
               .IsRequired()
               .HasMaxLength(20);

        builder.Property(t => t.Type)
               .IsRequired()
               .HasMaxLength(20);

        builder.Property(t => t.CreatedAt)
               .IsRequired()
               .HasColumnType("datetime")
               .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
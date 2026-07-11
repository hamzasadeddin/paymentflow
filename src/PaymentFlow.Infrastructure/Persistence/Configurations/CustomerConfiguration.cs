using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(256);
        builder.Property(c => c.PhoneNumber).HasMaxLength(32);
        builder.Property(c => c.CustomerReference).HasMaxLength(32).IsRequired();
        builder.Property(c => c.CountryCode).HasMaxLength(2);
        builder.Property(c => c.Type).HasConversion<int>();
        builder.Property(c => c.Status).HasConversion<int>();
        builder.Property(c => c.RowVersion).IsConcurrencyToken();

        builder.HasIndex(c => c.CustomerReference).IsUnique();
        builder.HasIndex(c => c.Name);
        builder.HasIndex(c => c.Status);

        builder.HasMany(c => c.Accounts)
            .WithOne(a => a.Customer!)
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Customer.Accounts))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class PaymentAccountConfiguration : IEntityTypeConfiguration<PaymentAccount>
{
    public void Configure(EntityTypeBuilder<PaymentAccount> builder)
    {
        builder.ToTable("PaymentAccounts");
        builder.Property(a => a.AccountNumber).HasMaxLength(34).IsRequired();
        builder.Property(a => a.Currency).HasMaxLength(3).IsRequired();
        builder.Property(a => a.Status).HasConversion<int>();

        // Money as decimal(19,4) — never floating point.
        builder.Property(a => a.AvailableBalance).HasColumnType("decimal(19,4)");
        builder.Property(a => a.LedgerBalance).HasColumnType("decimal(19,4)");
        builder.Property(a => a.DailyLimit).HasColumnType("decimal(19,4)");
        builder.Property(a => a.RowVersion).IsConcurrencyToken();

        builder.Ignore(a => a.MaskedNumber);
        builder.HasIndex(a => a.CustomerId);
    }
}

public sealed class BeneficiaryConfiguration : IEntityTypeConfiguration<Beneficiary>
{
    public void Configure(EntityTypeBuilder<Beneficiary> builder)
    {
        builder.ToTable("Beneficiaries");
        builder.Property(b => b.Name).HasMaxLength(200).IsRequired();
        builder.Property(b => b.AccountNumber).HasMaxLength(34).IsRequired();
        builder.Property(b => b.BankName).HasMaxLength(200);
        builder.Property(b => b.BankIdentifierCode).HasMaxLength(11);
        builder.Property(b => b.Currency).HasMaxLength(3).IsRequired();
        builder.Property(b => b.CountryCode).HasMaxLength(2);
        builder.Property(b => b.Status).HasConversion<int>();
        builder.Property(b => b.ReviewedByUserId).HasMaxLength(64);
        builder.Property(b => b.ReviewNotes).HasMaxLength(1024);
        builder.Property(b => b.RowVersion).IsConcurrencyToken();

        builder.Ignore(b => b.MaskedNumber);
        builder.Ignore(b => b.CanEdit);
        builder.HasIndex(b => b.CustomerId);
        builder.HasIndex(b => b.Status);

        builder.HasOne(b => b.Customer)
            .WithMany()
            .HasForeignKey(b => b.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

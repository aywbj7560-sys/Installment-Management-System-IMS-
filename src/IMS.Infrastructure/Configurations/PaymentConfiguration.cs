using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", "public", table =>
        {
            table.HasCheckConstraint("payments_amount_check", "amount > 0.00");
        });
        builder.HasKey(x => x.PaymentId).HasName("payments_pkey");

        builder.Property(x => x.PaymentId)
            .HasColumnName("payment_id")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn()
            .IsRequired(true);

        builder.Property(x => x.PaymentReference)
            .HasColumnName("payment_reference")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .IsRequired(true);

        builder.HasIndex(x => x.PaymentReference).IsUnique().HasDatabaseName("payments_payment_reference_key");

        builder.Property(x => x.ContractId)
            .HasColumnName("contract_id")
            .HasColumnType("bigint")
            .IsRequired(true);

        builder.Property(x => x.ReceivedByUserId)
            .HasColumnName("received_by_user_id")
            .HasColumnType("bigint")
            .IsRequired(true);

        builder.Property(x => x.PaymentDate)
            .HasColumnName("payment_date")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired(true);

        builder.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(15,2)")
            .HasPrecision(15, 2)
            .IsRequired(true);

        builder.Property(x => x.PaymentMethod)
            .HasColumnName("payment_method")
            .HasColumnType("varchar(30)")
            .HasMaxLength(30)
            .IsRequired(true);

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasColumnType("text")
            .IsRequired(false);

        builder.HasOne(x => x.Contract)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("payments_contract_id_fkey");

        builder.HasOne(x => x.ReceivedByUser)
            .WithMany(x => x.ReceivedPayments)
            .HasForeignKey(x => x.ReceivedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("payments_received_by_user_id_fkey");

        builder.HasIndex(x => x.ContractId)
            .HasDatabaseName("idx_payments_contract_id");

        builder.HasIndex(x => x.ReceivedByUserId)
            .HasDatabaseName("idx_payments_received_by_user_id");

        builder.HasIndex(x => x.PaymentDate)
            .HasDatabaseName("idx_payments_payment_date");
    }
}

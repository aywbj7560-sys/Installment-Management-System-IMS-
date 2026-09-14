using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Configurations;

public class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("payment_allocations", "public", table =>
        {
            table.HasCheckConstraint("payment_allocations_allocated_amount_check", "allocated_amount > 0.00");
            table.HasTrigger("trg_validate_payment_allocation_contract");
        });
        builder.HasKey(x => x.PaymentAllocationId).HasName("payment_allocations_pkey");

        builder.Property(x => x.PaymentAllocationId)
            .HasColumnName("payment_allocation_id")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn()
            .IsRequired(true);

        builder.Property(x => x.PaymentId)
            .HasColumnName("payment_id")
            .HasColumnType("bigint")
            .IsRequired(true);

        builder.Property(x => x.InstallmentId)
            .HasColumnName("installment_id")
            .HasColumnType("bigint")
            .IsRequired(true);

        builder.Property(x => x.AllocatedAmount)
            .HasColumnName("allocated_amount")
            .HasColumnType("numeric(15,2)")
            .HasPrecision(15, 2)
            .IsRequired(true);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired(true);

        builder.HasOne(x => x.Payment)
            .WithMany(x => x.PaymentAllocations)
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("payment_allocations_payment_id_fkey");

        builder.HasOne(x => x.Installment)
            .WithMany(x => x.PaymentAllocations)
            .HasForeignKey(x => x.InstallmentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("payment_allocations_installment_id_fkey");

        builder.HasIndex(x => x.InstallmentId)
            .HasDatabaseName("idx_payment_allocations_installment_id");

        builder.HasIndex(x => new { x.PaymentId, x.InstallmentId }).IsUnique()
            .HasDatabaseName("uq_payment_allocations_payment_installment");
    }
}

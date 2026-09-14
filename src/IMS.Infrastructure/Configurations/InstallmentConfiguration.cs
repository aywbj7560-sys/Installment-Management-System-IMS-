using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Configurations;

public class InstallmentConfiguration : IEntityTypeConfiguration<Installment>
{
    public void Configure(EntityTypeBuilder<Installment> builder)
    {
        builder.ToTable("installments", "public", table =>
        {
            table.HasCheckConstraint("installments_installment_number_check", "installment_number BETWEEN 1 AND 12");
            table.HasCheckConstraint("installments_amount_check", "amount > 0.00");
            table.HasCheckConstraint("installments_paid_amount_check", "paid_amount >= 0.00");
            table.HasCheckConstraint("installments_remaining_amount_check", "remaining_amount >= 0.00");
            table.HasCheckConstraint("installments_status_check", "status IN ('Pending', 'Paid', 'Partially Paid', 'Overdue', 'Waived')");
            table.HasCheckConstraint("chk_installments_paid_amount", "paid_amount <= amount");
        });
        builder.HasKey(x => x.InstallmentId).HasName("installments_pkey");

        builder.Property(x => x.InstallmentId)
            .HasColumnName("installment_id")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn()
            .IsRequired(true);

        builder.Property(x => x.ContractId)
            .HasColumnName("contract_id")
            .HasColumnType("bigint")
            .IsRequired(true);

        builder.Property(x => x.InstallmentNumber)
            .HasColumnName("installment_number")
            .HasColumnType("integer")
            .IsRequired(true);

        builder.Property(x => x.DueDate)
            .HasColumnName("due_date")
            .HasColumnType("date")
            .IsRequired(true);

        builder.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(15,2)")
            .HasPrecision(15, 2)
            .IsRequired(true);

        builder.Property(x => x.PaidAmount)
            .HasColumnName("paid_amount")
            .HasColumnType("numeric(15,2)")
            .HasPrecision(15, 2)
            .HasDefaultValue(0m)
            .IsRequired(true);

        builder.Property(x => x.RemainingAmount)
            .HasColumnName("remaining_amount")
            .HasColumnType("numeric(15,2)")
            .HasPrecision(15, 2)
            .IsRequired(true);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .HasConversion(InstallmentStatusConversion.Converter)
            .HasDefaultValue(InstallmentStatus.Pending)
            .IsRequired(true);

        builder.HasOne(x => x.Contract)
            .WithMany(x => x.Installments)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("installments_contract_id_fkey");

        builder.HasIndex(x => x.DueDate)
            .HasDatabaseName("idx_installments_due_date");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("idx_installments_status");

        builder.HasIndex(x => new { x.ContractId, x.Status })
            .HasDatabaseName("idx_installments_contract_status");

        builder.HasIndex(x => new { x.ContractId, x.InstallmentNumber }).IsUnique()
            .HasDatabaseName("uq_installments_contract_installment_number");
    }
}

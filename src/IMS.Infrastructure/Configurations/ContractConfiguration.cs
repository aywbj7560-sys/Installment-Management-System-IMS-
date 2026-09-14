using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Configurations;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("contracts", "public", table =>
        {
            table.HasCheckConstraint("contracts_total_amount_check", "total_amount > 0.00");
            table.HasCheckConstraint("contracts_down_payment_check", "down_payment >= 0.00");
            table.HasCheckConstraint("contracts_remaining_amount_check", "remaining_amount >= 0.00");
            table.HasCheckConstraint("contracts_number_of_installments_check", "number_of_installments = 12");
            table.HasCheckConstraint("contracts_status_check", "status IN ('Draft', 'Active', 'Completed', 'Voided', 'Defaulted')");
            table.HasCheckConstraint("chk_contracts_financial_amounts", "down_payment <= total_amount AND remaining_amount <= total_amount");
        });
        builder.HasKey(x => x.ContractId).HasName("contracts_pkey");

        builder.Property(x => x.ContractId)
            .HasColumnName("contract_id")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn()
            .IsRequired(true);

        builder.Property(x => x.ContractNumber)
            .HasColumnName("contract_number")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .IsRequired(true);

        builder.HasIndex(x => x.ContractNumber).IsUnique().HasDatabaseName("contracts_contract_number_key");

        builder.Property(x => x.CustomerId)
            .HasColumnName("customer_id")
            .HasColumnType("bigint")
            .IsRequired(true);

        builder.Property(x => x.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .HasColumnType("bigint")
            .IsRequired(true);

        builder.Property(x => x.ContractDate)
            .HasColumnName("contract_date")
            .HasColumnType("timestamp with time zone")
            .IsRequired(true);

        builder.Property(x => x.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("numeric(15,2)")
            .HasPrecision(15, 2)
            .IsRequired(true);

        builder.Property(x => x.DownPayment)
            .HasColumnName("down_payment")
            .HasColumnType("numeric(15,2)")
            .HasPrecision(15, 2)
            .IsRequired(true);

        builder.Property(x => x.RemainingAmount)
            .HasColumnName("remaining_amount")
            .HasColumnType("numeric(15,2)")
            .HasPrecision(15, 2)
            .IsRequired(true);

        builder.Property(x => x.NumberOfInstallments)
            .HasColumnName("number_of_installments")
            .HasColumnType("integer")
            .HasDefaultValue(12)
            .IsRequired(true);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(ContractStatus.Draft)
            .IsRequired(true);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired(true);

        builder.HasOne(x => x.Customer)
            .WithMany(x => x.Contracts)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("contracts_customer_id_fkey");

        builder.HasOne(x => x.CreatedByUser)
            .WithMany(x => x.CreatedContracts)
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("contracts_created_by_user_id_fkey");

        builder.HasIndex(x => x.CustomerId)
            .HasDatabaseName("idx_contracts_customer_id");

        builder.HasIndex(x => x.CreatedByUserId)
            .HasDatabaseName("idx_contracts_created_by_user_id");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("idx_contracts_status");

        builder.HasIndex(x => x.ContractDate)
            .HasDatabaseName("idx_contracts_contract_date");
    }
}

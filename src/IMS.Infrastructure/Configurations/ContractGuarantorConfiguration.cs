using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Configurations;

public class ContractGuarantorConfiguration : IEntityTypeConfiguration<ContractGuarantor>
{
    public void Configure(EntityTypeBuilder<ContractGuarantor> builder)
    {
        builder.ToTable("contract_guarantors", "public");
        builder.HasKey(x => x.ContractGuarantorId).HasName("contract_guarantors_pkey");

        builder.Property(x => x.ContractGuarantorId)
            .HasColumnName("contract_guarantor_id")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn()
            .IsRequired(true);

        builder.Property(x => x.ContractId)
            .HasColumnName("contract_id")
            .HasColumnType("bigint")
            .IsRequired(true);

        builder.Property(x => x.GuarantorId)
            .HasColumnName("guarantor_id")
            .HasColumnType("bigint")
            .IsRequired(true);

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired(true);

        builder.HasOne(x => x.Contract)
            .WithMany(x => x.ContractGuarantors)
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("contract_guarantors_contract_id_fkey");

        builder.HasOne(x => x.Guarantor)
            .WithMany(x => x.ContractGuarantors)
            .HasForeignKey(x => x.GuarantorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("contract_guarantors_guarantor_id_fkey");

        builder.HasIndex(x => x.GuarantorId)
            .HasDatabaseName("idx_contract_guarantors_guarantor_id");

        builder.HasIndex(x => new { x.ContractId, x.GuarantorId }).IsUnique()
            .HasDatabaseName("uq_contract_guarantors_contract_guarantor");
    }
}

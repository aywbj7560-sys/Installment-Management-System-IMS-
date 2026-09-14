namespace IMS.Domain.Entities;

public class ContractGuarantor
{
    public long ContractGuarantorId { get; set; }
    public long ContractId { get; set; }
    public long GuarantorId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public Contract Contract { get; set; } = null!;

    public Guarantor Guarantor { get; set; } = null!;
}

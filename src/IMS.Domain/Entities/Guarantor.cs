namespace IMS.Domain.Entities;

public class Guarantor
{
    public long GuarantorId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string IdentificationNumber { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? SecondaryPhone { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? Occupation { get; set; }
    public string? Workplace { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<ContractGuarantor> ContractGuarantors { get; set; } = new List<ContractGuarantor>();
}

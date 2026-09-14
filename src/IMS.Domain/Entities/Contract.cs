using IMS.Domain.Enums;

namespace IMS.Domain.Entities;

public class Contract
{
    public long ContractId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public long CreatedByUserId { get; set; }
    public DateTime ContractDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DownPayment { get; set; }
    public decimal RemainingAmount { get; set; }
    public int NumberOfInstallments { get; set; } = 12;
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public DateTime CreatedAt { get; set; }

    public Customer Customer { get; set; } = null!;

    public User CreatedByUser { get; set; } = null!;

    public ICollection<ContractItem> ContractItems { get; set; } = new List<ContractItem>();

    public ICollection<ContractGuarantor> ContractGuarantors { get; set; } = new List<ContractGuarantor>();

    public ICollection<Installment> Installments { get; set; } = new List<Installment>();

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

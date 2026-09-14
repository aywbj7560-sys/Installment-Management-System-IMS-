using IMS.Domain.Enums;

namespace IMS.Domain.Entities;

public class Installment
{
    public long InstallmentId { get; set; }
    public long ContractId { get; set; }
    public int InstallmentNumber { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public InstallmentStatus Status { get; set; } = InstallmentStatus.Pending;

    public Contract Contract { get; set; } = null!;

    public ICollection<PaymentAllocation> PaymentAllocations { get; set; } = new List<PaymentAllocation>();
}

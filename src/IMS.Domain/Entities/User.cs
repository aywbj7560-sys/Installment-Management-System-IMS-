namespace IMS.Domain.Entities;

public class User
{
    public long UserId { get; set; }
    public long RoleId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public Role Role { get; set; } = null!;

    public ICollection<Contract> CreatedContracts { get; set; } = new List<Contract>();

    public ICollection<Payment> ReceivedPayments { get; set; } = new List<Payment>();
}

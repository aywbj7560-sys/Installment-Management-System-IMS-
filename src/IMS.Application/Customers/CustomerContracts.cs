using System.ComponentModel.DataAnnotations;
using IMS.Domain.Enums;

namespace IMS.Application.Customers;

public sealed class CustomerRequest
{
    [Required, StringLength(150)] public string FullName { get; init; } = string.Empty;
    [Required, StringLength(50)] public string IdentificationNumber { get; init; } = string.Empty;
    [Required, StringLength(20)] public string Phone { get; init; } = string.Empty;
    [StringLength(20)] public string? SecondaryPhone { get; init; }
    [StringLength(100), EmailAddress] public string? Email { get; init; }
    [Required] public string Address { get; init; } = string.Empty;
    [Required, RegularExpression("^(Active|Inactive|Blacklisted)$")]
    public string Status { get; init; } = "Active";

    public Dictionary<string, string[]> Validate()
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(this, new ValidationContext(this), results, true);
        var errors = results.SelectMany(r => r.MemberNames.Select(n => (Name: n, Message: r.ErrorMessage!)))
            .GroupBy(r => r.Name).ToDictionary(g => g.Key, g => g.Select(r => r.Message).ToArray());
        foreach (var property in GetType().GetProperties().Where(p => p.PropertyType == typeof(string)))
            if (property.GetValue(this) is string value && value.Contains('\0'))
                errors[property.Name] = ["Null characters are not allowed."];
        return errors;
    }
}

public sealed record CustomerResponse(long CustomerId, string FullName, string IdentificationNumber,
    string Phone, string? SecondaryPhone, string? Email, string Address, string Status, DateTime CreatedAt);
public sealed record CustomerPage(IReadOnlyList<CustomerResponse> Items, int TotalCount, int Page, int PageSize);
public enum CustomerWriteError { None, NotFound, DuplicateIdentification, ImmutableIdentity }
public sealed record CustomerWriteResult(CustomerResponse? Customer, CustomerWriteError Error = CustomerWriteError.None);

public interface ICustomerService
{
    Task<CustomerPage> ListAsync(string? search, CustomerStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<CustomerResponse?> GetAsync(long id, CancellationToken cancellationToken);
    Task<CustomerWriteResult> SaveAsync(long? id, CustomerRequest request, CancellationToken cancellationToken);
}

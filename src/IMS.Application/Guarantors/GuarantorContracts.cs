using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using IMS.Application.Contracts;

namespace IMS.Application.Guarantors;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class GuarantorRequest
{
    [Required, StringLength(150)] public string FullName { get; init; } = "";
    [Required, StringLength(50)] public string IdentificationNumber { get; init; } = "";
    [Required, StringLength(20)] public string Phone { get; init; } = "";
    [StringLength(20)] public string? SecondaryPhone { get; init; }
    [Required] public string Address { get; init; } = "";
    [StringLength(100)] public string? Occupation { get; init; }
    [StringLength(150)] public string? Workplace { get; init; }
    public string? Notes { get; init; }
    // Omission on PUT preserves status rather than inadvertently reactivating a guarantor.
    public bool? IsActive { get; init; }

    public Dictionary<string, string[]> Validate()
    {
        var errors = ContractValidation.Fields(this);
        foreach (var (name, value) in new[] { (nameof(Phone), Phone), (nameof(SecondaryPhone), SecondaryPhone) })
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            var phone = value.Trim();
            // No country-specific numbering rule exists. Accept digits and common separators.
            if (!phone.Any(c => c is >= '0' and <= '9') ||
                phone.Where((c, i) => !(c is >= '0' and <= '9' || c is ' ' or '-' or '(' or ')' || c == '+' && i == 0)).Any())
                errors[name] = ["Use digits, spaces, parentheses, hyphens and an optional leading +."];
        }
        return errors;
    }
}

public sealed record GuarantorResponse(long GuarantorId, string FullName, string IdentificationNumber,
    string Phone, string? SecondaryPhone, string Address, string? Occupation, string? Workplace,
    string? Notes, bool IsActive, DateTime CreatedAt);
public sealed record GuarantorContractSummary(long ContractGuarantorId, long ContractId, string ContractNumber,
    string Status, string? GuaranteeNotes, DateTime LinkedAt);
public sealed record GuarantorDetails(GuarantorResponse Guarantor, IReadOnlyList<GuarantorContractSummary> Contracts);
public sealed record GuarantorPage(IReadOnlyList<GuarantorResponse> Items, int TotalCount, int Page, int PageSize);
public sealed record GuarantorResult(GuarantorResponse? Guarantor, int ErrorStatus = 0, string? Message = null);

public interface IGuarantorService
{
    Task<GuarantorPage> ListAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken ct);
    Task<GuarantorDetails?> GetAsync(long id, CancellationToken ct);
    Task<GuarantorResult> SaveAsync(long? id, GuarantorRequest request, CancellationToken ct);
}

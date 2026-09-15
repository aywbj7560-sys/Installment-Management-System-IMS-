using System.ComponentModel.DataAnnotations;

namespace IMS.Application.Products;

public sealed class ProductRequest
{
    [Required, StringLength(50)] public string ProductCode { get; init; } = string.Empty;
    [Required, StringLength(150)] public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    [Required] public decimal? CashPrice { get; init; }
    public decimal? InstallmentPrice { get; init; }
    public bool IsActive { get; init; } = true;

    public Dictionary<string, string[]> Validate()
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(this, new ValidationContext(this), results, true);
        var errors = results.SelectMany(r => r.MemberNames.Select(n => (Name: n, Message: r.ErrorMessage!)))
            .GroupBy(r => r.Name).ToDictionary(g => g.Key, g => g.Select(r => r.Message).ToArray());
        foreach (var (name, price) in new[] { (nameof(CashPrice), CashPrice), (nameof(InstallmentPrice), InstallmentPrice) })
            if (price is decimal value && (value < 0 || value > 9999999999999.99m || decimal.Round(value, 2) != value))
                errors[name] = ["Use a price from 0 to 9999999999999.99 with at most two decimal places."];
        foreach (var (name, value) in new[] { (nameof(ProductCode), ProductCode), (nameof(Name), Name), (nameof(Description), Description) })
            if (value?.Contains('\0') == true) errors[name] = ["Null characters are not allowed."];
        return errors;
    }
}

public sealed record ProductResponse(long ProductId, string ProductCode, string Name, string? Description,
    decimal CashPrice, decimal? InstallmentPrice, bool IsActive, DateTime CreatedAt);
public sealed record ProductPage(IReadOnlyList<ProductResponse> Items, int TotalCount, int Page, int PageSize);
public enum ProductWriteError { None, NotFound, DuplicateCode }
public sealed record ProductWriteResult(ProductResponse? Product, ProductWriteError Error = ProductWriteError.None);
public interface IProductService
{
    Task<ProductPage> ListAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken cancellationToken);
    Task<ProductResponse?> GetAsync(long id, CancellationToken cancellationToken);
    Task<ProductWriteResult> SaveAsync(long? id, ProductRequest request, CancellationToken cancellationToken);
}

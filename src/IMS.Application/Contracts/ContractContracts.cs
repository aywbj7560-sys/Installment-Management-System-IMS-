using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using IMS.Domain.Enums;
namespace IMS.Application.Contracts;

public sealed class ContractRequest
{
    [Required, StringLength(50)] public string ContractNumber { get; init; } = "";
    [Range(1,long.MaxValue)] public long CustomerId { get; init; }
    public DateTimeOffset? ContractDate { get; init; }
    public decimal DownPayment { get; init; }
    public List<ContractItemRequest?>? Items { get; init; }
    public long? GuarantorId { get; init; }
    public NewGuarantorRequest? Guarantor { get; init; }
    public string? GuaranteeNotes { get; init; }
    public Dictionary<string,string[]> Validate()
    {
        var errors = ContractValidation.Fields(this);
        if (ContractDate is null || ContractDate.Value.UtcDateTime.Year > 9998) errors["ContractDate"] = ["Provide a contract date allowing twelve subsequent months."];
        if (!ContractValidation.Money(DownPayment)) errors["DownPayment"] = ["Use a nonnegative numeric(15,2) amount."];
        if (Items is null || Items.Count == 0) errors["Items"] = ["At least one item is required."];
        else
        {
            for (var i=0;i<Items.Count;i++)
            {
                var item=Items[i];
                if (item is null || item.ProductId <= 0 || item.Quantity <= 0 || item.UnitPrice is null || !ContractValidation.Money(item.UnitPrice.Value))
                    errors[$"Items[{i}]"] = ["Positive product ID/quantity and a nonnegative numeric(15,2) unit price are required."];
            }
            if (Items.Where(x=>x is not null).GroupBy(x=>x!.ProductId).Any(g=>g.Count()>1)) errors["Items"]=["Combine duplicate products into one item."];
        }
        if ((GuarantorId.HasValue == (Guarantor is not null)) || GuarantorId <= 0) errors["Guarantor"]=["Provide either a positive existing guarantor ID or new guarantor details."];
        if (Guarantor is not null) foreach(var error in ContractValidation.Fields(Guarantor)) errors["Guarantor."+error.Key]=error.Value;
        return errors;
    }
}
public sealed record ContractItemRequest(long ProductId, int Quantity, decimal? UnitPrice);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ActivateContractRequest
{
    public bool DownPaymentConfirmed { get; init; }
    [StringLength(100)] public string? Reference { get; init; }
    public string? Note { get; init; }
    public Dictionary<string,string[]> Validate()
    {
        var errors = ContractValidation.Fields(this);
        if (!DownPaymentConfirmed) errors[nameof(DownPaymentConfirmed)] = ["Confirm receipt of the agreed down payment."];
        return errors;
    }
}
public sealed class NewGuarantorRequest
{
    [Required,StringLength(150)] public string FullName {get;init;}="";
    [Required,StringLength(50)] public string IdentificationNumber {get;init;}="";
    [Required,StringLength(20)] public string Phone {get;init;}="";
    [StringLength(20)] public string? SecondaryPhone {get;init;}
    [Required] public string Address {get;init;}="";
    [StringLength(100)] public string? Occupation {get;init;}
    [StringLength(150)] public string? Workplace {get;init;}
    public string? Notes {get;init;}
}
public static class ContractValidation
{
    public const decimal MaxMoney=9999999999999.99m;
    public static bool Money(decimal value)=>value>=0 && value<=MaxMoney && decimal.Round(value,2)==value;
    public static Dictionary<string,string[]> Fields(object value)
    {
        var results=new List<ValidationResult>();
        Validator.TryValidateObject(value,new ValidationContext(value),results,true);
        var errors=results.SelectMany(r=>r.MemberNames.Select(n=>(Name:n,Message:r.ErrorMessage!))).GroupBy(r=>r.Name).ToDictionary(g=>g.Key,g=>g.Select(r=>r.Message).ToArray());
        foreach(var p in value.GetType().GetProperties()) if(p.GetValue(value) is string s && s.Contains('\0')) errors[p.Name]=["Null characters are not allowed."];
        return errors;
    }
    public static decimal[] Schedule(decimal principal)
    {
        var standard=decimal.Floor(principal*100/12)/100;
        return Enumerable.Range(1,12).Select(i=>i==12 ? principal-standard*11 : standard).ToArray();
    }
}
public sealed record ContractSummary(long ContractId,string ContractNumber,long CustomerId,string CustomerName,long CreatedByUserId,DateTime ContractDate,decimal TotalAmount,decimal DownPayment,decimal RemainingAmount,int NumberOfInstallments,string Status,DateTime CreatedAt);
public sealed record ContractItemResponse(long ContractItemId,long ProductId,string ProductCode,string ProductName,int Quantity,decimal UnitPrice,decimal Subtotal);
public sealed record ContractGuarantorResponse(long GuarantorId,string FullName,string IdentificationNumber,string Phone,string? SecondaryPhone,string Address,string? Occupation,string? Workplace,string? Notes,bool IsActive,string? GuaranteeNotes);
public sealed record InstallmentResponse(long InstallmentId,int InstallmentNumber,DateOnly DueDate,decimal Amount,decimal PaidAmount,decimal RemainingAmount,string Status);
public sealed record ContractDetails(ContractSummary Contract,IReadOnlyList<ContractItemResponse> Items,IReadOnlyList<ContractGuarantorResponse> Guarantors,IReadOnlyList<InstallmentResponse> Installments);
public sealed record ContractPage(IReadOnlyList<ContractSummary> Items,int TotalCount,int Page,int PageSize);
public sealed record ContractResult(ContractDetails? Contract,int ErrorStatus=0,string? Message=null);
public interface IContractService
{
    Task<ContractResult> CreateAsync(ContractRequest request,long userId,CancellationToken cancellationToken);
    Task<ContractResult> ActivateAsync(long id,ActivateContractRequest request,long userId,CancellationToken cancellationToken);
    Task<ContractDetails?> GetAsync(long id,CancellationToken cancellationToken);
    Task<ContractPage> ListAsync(string? search,long? customerId,ContractStatus? status,int page,int pageSize,CancellationToken cancellationToken);
}

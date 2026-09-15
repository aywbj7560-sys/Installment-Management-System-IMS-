using System.Data;
using IMS.Application.Contracts;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace IMS.Infrastructure.Contracts;

public sealed class ContractService(ImsDbContext db) : IContractService
{
    public async Task<ContractResult> CreateAsync(ContractRequest request,long userId,CancellationToken ct)
    {
        if(request.Validate().Count>0) return new(null,400,"Invalid contract request.");
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            var customer=await db.Customers.SingleOrDefaultAsync(x=>x.CustomerId==request.CustomerId,ct);
            if(customer is null) return new(null,404,"Customer not found.");
            if(customer.Status!=CustomerStatus.Active) return new(null,409,"Customer must be active.");
            if(!await db.Users.AnyAsync(x=>x.UserId==userId && x.IsActive,ct)) return new(null,403,"Contract creator is unavailable.");
            if(await db.Contracts.AnyAsync(x=>x.ContractNumber==request.ContractNumber.Trim(),ct)) return new(null,409,"Contract number already exists.");
            var ids=request.Items!.Select(x=>x!.ProductId).ToArray();
            var products=await db.Products.Where(x=>ids.Contains(x.ProductId)).ToDictionaryAsync(x=>x.ProductId,ct);
            if(products.Count!=ids.Length) return new(null,404,"Product not found.");
            if(products.Values.Any(x=>!x.IsActive)) return new(null,409,"Products must be active.");
            var items=request.Items!.Select(x=>new ContractItem { ProductId=x!.ProductId,Product=products[x.ProductId],Quantity=x.Quantity,UnitPrice=x.UnitPrice!.Value,Subtotal=x.UnitPrice.Value*x.Quantity }).ToList();
            var total=items.Sum(x=>x.Subtotal);
            if(total>ContractValidation.MaxMoney || total<=0 || items.Any(x=>x.Subtotal>ContractValidation.MaxMoney) || total-request.DownPayment<0.12m)
                return new(null,400,"Total must fit numeric(15,2) and financed principal must be at least 0.12 for twelve positive installments.");
            Guarantor? guarantor;
            if(request.GuarantorId.HasValue)
            {
                guarantor=await db.Guarantors.SingleOrDefaultAsync(x=>x.GuarantorId==request.GuarantorId.Value,ct);
                if(guarantor is null) return new(null,404,"Guarantor not found.");
                if(!guarantor.IsActive) return new(null,409,"Guarantor must be active.");
            }
            else
            {
                var g=request.Guarantor!;
                if(await db.Guarantors.AnyAsync(x=>x.IdentificationNumber==g.IdentificationNumber.Trim(),ct)) return new(null,409,"Guarantor identification already exists; use the existing guarantor ID.");
                guarantor=new Guarantor { FullName=g.FullName.Trim(),IdentificationNumber=g.IdentificationNumber.Trim(),Phone=g.Phone.Trim(),SecondaryPhone=Optional(g.SecondaryPhone),Address=g.Address.Trim(),Occupation=Optional(g.Occupation),Workplace=Optional(g.Workplace),Notes=Optional(g.Notes),IsActive=true };
            }
            var contract=new Contract { ContractNumber=request.ContractNumber.Trim(),CustomerId=customer.CustomerId,Customer=customer,CreatedByUserId=userId,ContractDate=request.ContractDate!.Value.UtcDateTime,TotalAmount=total,DownPayment=request.DownPayment,RemainingAmount=total-request.DownPayment,NumberOfInstallments=12,Status=ContractStatus.Draft,ContractItems=items };
            contract.ContractGuarantors.Add(new ContractGuarantor { Guarantor=guarantor,Notes=Optional(request.GuaranteeNotes) });
            db.Contracts.Add(contract);
            // Flush the aggregate before its schedule; both writes remain in the explicit transaction.
            await db.SaveChangesAsync(ct);
            var amounts=ContractValidation.Schedule(contract.RemainingAmount);
            var anchor=DateOnly.FromDateTime(contract.ContractDate);
            for(var i=1;i<=12;i++) contract.Installments.Add(new Installment { InstallmentNumber=i,DueDate=anchor.AddMonths(i),Amount=amounts[i-1],RemainingAmount=amounts[i-1],PaidAmount=0,Status=InstallmentStatus.Pending });
            await db.SaveChangesAsync(ct);
            var result=await GetAsync(contract.ContractId,ct);
            await transaction.CommitAsync(ct);
            return new(result);
        }
        catch(Exception ex) when (ex is PostgresException || ex is DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            var pg=ex as PostgresException ?? ex.InnerException as PostgresException;
            if(pg?.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
                return new(null,409,"Contract creation conflicted with existing or concurrently changed data. Review the request and retry.");
            throw;
        }
        // Disposal rolls back validation returns, cancellation and unexpected exceptions.
    }
    public async Task<ContractPage> ListAsync(string? search,long? customerId,ContractStatus? status,int page,int pageSize,CancellationToken ct)
    {
        var query=db.Contracts.AsNoTracking();
        if(customerId.HasValue) query=query.Where(x=>x.CustomerId==customerId.Value);
        if(status.HasValue) query=query.Where(x=>x.Status==status.Value);
        if(!string.IsNullOrWhiteSpace(search)) { var term=search.Trim().ToLowerInvariant(); query=query.Where(x=>x.ContractNumber.ToLower().Contains(term)||x.Customer.FullName.ToLower().Contains(term)); }
        var count=await query.CountAsync(ct);
        var rows=await query.Include(x=>x.Customer).OrderBy(x=>x.ContractId).Skip((page-1)*pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(Summary).ToArray(),count,page,pageSize);
    }
    public async Task<ContractDetails?> GetAsync(long id,CancellationToken ct)
    {
        var c=await db.Contracts.AsNoTracking().Include(x=>x.Customer).Include(x=>x.ContractItems).ThenInclude(x=>x.Product).Include(x=>x.ContractGuarantors).ThenInclude(x=>x.Guarantor).Include(x=>x.Installments).SingleOrDefaultAsync(x=>x.ContractId==id,ct);
        if(c is null) return null;
        return new(Summary(c),c.ContractItems.OrderBy(x=>x.ContractItemId).Select(x=>new ContractItemResponse(x.ContractItemId,x.ProductId,x.Product.ProductCode,x.Product.Name,x.Quantity,x.UnitPrice,x.Subtotal)).ToArray(),c.ContractGuarantors.Select(x=>new ContractGuarantorResponse(x.GuarantorId,x.Guarantor.FullName,x.Guarantor.IdentificationNumber,x.Guarantor.Phone,x.Guarantor.SecondaryPhone,x.Guarantor.Address,x.Guarantor.Occupation,x.Guarantor.Workplace,x.Guarantor.Notes,x.Guarantor.IsActive,x.Notes)).ToArray(),c.Installments.OrderBy(x=>x.InstallmentNumber).Select(x=>new InstallmentResponse(x.InstallmentId,x.InstallmentNumber,x.DueDate,x.Amount,x.PaidAmount,x.RemainingAmount,x.Status.ToString())).ToArray());
    }
    private static ContractSummary Summary(Contract c)=>new(c.ContractId,c.ContractNumber,c.CustomerId,c.Customer.FullName,c.CreatedByUserId,c.ContractDate,c.TotalAmount,c.DownPayment,c.RemainingAmount,c.NumberOfInstallments,c.Status.ToString(),c.CreatedAt);
    private static string? Optional(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
}


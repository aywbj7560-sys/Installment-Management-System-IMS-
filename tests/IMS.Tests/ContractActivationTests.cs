using IMS.Application.Contracts;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Contracts;
using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IMS.Tests;

public class ContractActivationTests
{
    private static async Task<(ImsDbContext Db, ContractService Service)> Setup(ContractStatus status=ContractStatus.Draft)
    {
        var db=new ImsDbContext(new DbContextOptionsBuilder<ImsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var role=new Role { RoleId=1,RoleName="Admin" };
        var user=new User { UserId=1,RoleId=1,Role=role,Username="admin",Email="admin@test",FullName="Admin",PasswordHash="x",IsActive=true };
        var customer=new Customer { CustomerId=1,FullName="Customer",IdentificationNumber="C-1",Phone="0",Address="Address",Status=CustomerStatus.Active };
        var product=new Product { ProductId=1,ProductCode="P-1",Name="Product",CashPrice=120,IsActive=true };
        var guarantor=new Guarantor { GuarantorId=1,FullName="Guarantor",IdentificationNumber="G-1",Phone="0",Address="Address",IsActive=true };
        var contract=new Contract { ContractId=1,ContractNumber="CNT-1",CustomerId=1,Customer=customer,CreatedByUserId=1,CreatedByUser=user,
            ContractDate=new DateTime(2028,1,31,0,0,0,DateTimeKind.Utc),TotalAmount=120,DownPayment=12,RemainingAmount=108,NumberOfInstallments=12,Status=status };
        contract.ContractItems.Add(new ContractItem { ContractItemId=1,ProductId=1,Product=product,Quantity=1,UnitPrice=120,Subtotal=120 });
        contract.ContractGuarantors.Add(new ContractGuarantor { ContractGuarantorId=1,GuarantorId=1,Guarantor=guarantor });
        foreach(var i in Enumerable.Range(1,12)) contract.Installments.Add(new Installment { InstallmentId=i,InstallmentNumber=i,
            DueDate=new DateOnly(2028,1,31).AddMonths(i),Amount=9,PaidAmount=0,RemainingAmount=9,Status=InstallmentStatus.Pending });
        db.Add(contract); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        return (db,new ContractService(db));
    }

    [Fact]
    public async Task ActivatesAndPreservesAggregateWithOneAudit()
    {
        var (db,service)=await Setup(); await using(db)
        {
            var before=await Snapshot(db);
            var start=DateTime.UtcNow;
            var result=await service.ActivateAsync(1,new ActivateContractRequest { DownPaymentConfirmed=true,Reference=" RECEIPT-1 ",Note=" confirmed " },1,default);
            Assert.Equal(0,result.ErrorStatus); Assert.Equal("Active",result.Contract!.Contract.Status);
            var after=await Snapshot(db); Assert.Equal(before with { Status=ContractStatus.Active },after);
            var audit=Assert.Single(await db.AuditLogs.AsNoTracking().ToListAsync());
            Assert.Equal(1,audit.UserId);Assert.Equal("ContractActivated",audit.ActionType);Assert.Equal("Contract",audit.TargetEntityType);Assert.Equal(1,audit.TargetEntityId);
            Assert.Equal("Draft",audit.PreviousState);Assert.Equal("Active",audit.NewState);Assert.Equal("RECEIPT-1",audit.Reference);Assert.Equal("confirmed",audit.Note);
            Assert.InRange(audit.Timestamp,start,DateTime.UtcNow);
        }
    }

    [Theory]
    [InlineData(ContractStatus.Active,"already active")]
    [InlineData(ContractStatus.Completed,"Completed")]
    [InlineData(ContractStatus.Voided,"Voided")]
    [InlineData(ContractStatus.Defaulted,"Defaulted")]
    public async Task RejectsNonDraftStates(ContractStatus status,string message)
    {
        var (db,service)=await Setup(status);await using(db)
        { var result=await service.ActivateAsync(1,new ActivateContractRequest { DownPaymentConfirmed=true },1,default);Assert.Equal(409,result.ErrorStatus);Assert.Contains(message,result.Message!,StringComparison.OrdinalIgnoreCase);Assert.Empty(db.AuditLogs); }
    }

    [Fact]
    public async Task RequiresConfirmationAndExistingContract()
    {
        var (db,service)=await Setup();await using(db)
        {
            Assert.Equal(400,(await service.ActivateAsync(1,new ActivateContractRequest(),1,default)).ErrorStatus);
            Assert.Equal(404,(await service.ActivateAsync(999,new ActivateContractRequest { DownPaymentConfirmed=true },1,default)).ErrorStatus);
            Assert.Empty(db.AuditLogs);
        }
    }

    public static IEnumerable<object[]> InvalidAggregates()
    {
        yield return ["inactive customer",(Action<ImsDbContext>)(db=>db.Customers.Single().Status=CustomerStatus.Inactive)];
        yield return ["inactive guarantor",(Action<ImsDbContext>)(db=>db.Guarantors.Single().IsActive=false)];
        yield return ["inactive product",(Action<ImsDbContext>)(db=>db.Products.Single().IsActive=false)];
        yield return ["missing product",(Action<ImsDbContext>)(db=>db.Products.Remove(db.Products.Single()))];
        yield return ["wrong installment count",(Action<ImsDbContext>)(db=>db.Installments.Remove(db.Installments.First()))];
        yield return ["non-pending installment",(Action<ImsDbContext>)(db=>db.Installments.First().Status=InstallmentStatus.Overdue)];
        yield return ["installment mismatch",(Action<ImsDbContext>)(db=>db.Installments.First().Amount=8)];
        yield return ["missing item",(Action<ImsDbContext>)(db=>db.ContractItems.Remove(db.ContractItems.Single()))];
        yield return ["item integrity",(Action<ImsDbContext>)(db=>db.ContractItems.Single().Subtotal=119)];
        yield return ["financial integrity",(Action<ImsDbContext>)(db=>db.Contracts.Single().RemainingAmount=107)];
    }

    [Theory,MemberData(nameof(InvalidAggregates))]
    public async Task RejectsIneligibleOrInconsistentAggregate(string _,Action<ImsDbContext> corrupt)
    {
        var (db,service)=await Setup();await using(db)
        { corrupt(db);await db.SaveChangesAsync();db.ChangeTracker.Clear();var result=await service.ActivateAsync(1,new ActivateContractRequest { DownPaymentConfirmed=true },1,default);Assert.Equal(409,result.ErrorStatus);Assert.Empty(db.AuditLogs);Assert.Equal(ContractStatus.Draft,(await db.Contracts.SingleAsync()).Status); }
    }

    private static async Task<AggregateSnapshot> Snapshot(ImsDbContext db)
    {
        var contract=await db.Contracts.AsNoTracking().SingleAsync();
        var items=await db.ContractItems.AsNoTracking().OrderBy(x=>x.ContractItemId).Select(x=>new {x.ContractItemId,x.ProductId,x.Quantity,x.UnitPrice,x.Subtotal}).ToListAsync();
        var links=await db.ContractGuarantors.AsNoTracking().OrderBy(x=>x.ContractGuarantorId).Select(x=>new {x.ContractGuarantorId,x.GuarantorId,x.Notes}).ToListAsync();
        var installments=await db.Installments.AsNoTracking().OrderBy(x=>x.InstallmentId).Select(x=>new {x.InstallmentId,x.InstallmentNumber,x.DueDate,x.Amount,x.PaidAmount,x.RemainingAmount,x.Status}).ToListAsync();
        return new(contract.Status,contract.TotalAmount,contract.DownPayment,contract.RemainingAmount,contract.ContractDate,
            System.Text.Json.JsonSerializer.Serialize(items),System.Text.Json.JsonSerializer.Serialize(links),System.Text.Json.JsonSerializer.Serialize(installments));
    }
    private sealed record AggregateSnapshot(ContractStatus Status,decimal TotalAmount,decimal DownPayment,decimal RemainingAmount,DateTime ContractDate,string Items,string Links,string Installments);
}

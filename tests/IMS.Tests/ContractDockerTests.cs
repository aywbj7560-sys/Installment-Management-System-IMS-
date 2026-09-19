using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IMS.Application.Authentication;
using IMS.Application.Contracts;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Contracts;
using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
namespace IMS.Tests;
public class ContractDockerTests
{
    [LiveCustomerFact]
    public async Task AtomicWorkflowAndRegression()
    {
        using var client=new HttpClient { BaseAddress=new Uri("http://api:8080") };
        var login=await client.PostAsJsonAsync("/api/auth/login",new LoginRequest { Email=Environment.GetEnvironmentVariable("SeedAdmin__Email")!,Password=Environment.GetEnvironmentVariable("SeedAdmin__Password")! });
        Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        var auth=(await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",auth.Token);
        var connection=Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        await using var db=new ImsDbContext(new DbContextOptionsBuilder<ImsDbContext>().UseNpgsql(connection).Options);
        var key="CTEST-"+Guid.NewGuid().ToString("N");
        var customer=new Customer { FullName=key,IdentificationNumber=key,Phone="0",Address="test" };
        var p1=new Product { ProductCode=key,Name="one",CashPrice=10 };
        var p2=new Product { ProductCode=key+"2",Name="two",CashPrice=20 };
        db.AddRange(customer,p1,p2);await db.SaveChangesAsync();
        ContractRequest Request(string suffix="",long? customerId=null,long? productId=null,long? guarantorId=null)=>new()
        { ContractNumber=key+suffix,CustomerId=customerId??customer.CustomerId,ContractDate=new DateTimeOffset(2028,1,31,0,0,0,TimeSpan.Zero),DownPayment=10,Items=[new(productId??p1.ProductId,2,25m),new(p2.ProductId,1,60m)],GuarantorId=guarantorId,Guarantor=guarantorId.HasValue?null:new NewGuarantorRequest { FullName="Guarantor",IdentificationNumber=key+suffix,Phone="0",Address="test" } };
        try
        {
            var created=await client.PostAsJsonAsync("/api/contracts",Request());
            Assert.Equal(HttpStatusCode.Created,created.StatusCode);
            var details=(await created.Content.ReadFromJsonAsync<ContractDetails>())!;
            var id=details.Contract.ContractId;
            Assert.Equal(customer.CustomerId,details.Contract.CustomerId);Assert.Equal(auth.User.Id,details.Contract.CreatedByUserId);
            Assert.Equal("Draft",details.Contract.Status);Assert.Equal(110m,details.Contract.TotalAmount);Assert.Equal(100m,details.Contract.RemainingAmount);
            Assert.Equal(2,details.Items.Count);Assert.Equal(50m,details.Items[0].Subtotal);Assert.Single(details.Guarantors);
            Assert.Equal(12,details.Installments.Count);Assert.Equal(Enumerable.Range(1,12),details.Installments.Select(x=>x.InstallmentNumber));
            Assert.Equal(details.Contract.TotalAmount,details.Installments.Sum(x=>x.Amount)+details.Contract.DownPayment);
            Assert.Equal(new DateOnly(2028,2,29),details.Installments[0].DueDate);Assert.Equal(new DateOnly(2028,3,31),details.Installments[1].DueDate);Assert.Equal(8.37m,details.Installments[11].Amount);
            Assert.All(details.Installments,x=>{Assert.Equal(0m,x.PaidAmount);Assert.Equal(x.Amount,x.RemainingAmount);Assert.Equal("Pending",x.Status);});
            Assert.Equal(12,await db.Installments.CountAsync(x=>x.ContractId==id));Assert.Equal(2,await db.ContractItems.CountAsync(x=>x.ContractId==id));Assert.Equal(1,await db.ContractGuarantors.CountAsync(x=>x.ContractId==id));
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(created.Headers.Location)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/contracts",Request())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync("/api/contracts",Request("missing-c",long.MaxValue))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync("/api/contracts",Request("missing-p",productId:long.MaxValue))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsJsonAsync("/api/contracts",Request("missing-g",guarantorId:long.MaxValue))).StatusCode);
            p1.IsActive=false;await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/contracts",Request("inactive"))).StatusCode);
            p1.IsActive=true;customer.Status=CustomerStatus.Blacklisted;await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/contracts",Request("blacklisted"))).StatusCode);
            customer.Status=CustomerStatus.Active;await db.SaveChangesAsync();
            var second=await client.PostAsJsonAsync("/api/contracts",Request("second",guarantorId:details.Guarantors[0].GuarantorId));
            Assert.Equal(HttpStatusCode.Created,second.StatusCode);
            var page=(await client.GetFromJsonAsync<ContractPage>($"/api/contracts?customerId={customer.CustomerId}&status=Draft&search={key}&pageSize=1"))!;
            Assert.Equal(2,page.TotalCount);Assert.Single(page.Items);
            var page2=(await client.GetFromJsonAsync<ContractPage>($"/api/contracts?customerId={customer.CustomerId}&pageSize=1&page=2"))!;
            Assert.Single(page2.Items);Assert.NotEqual(page.Items[0].ContractId,page2.Items[0].ContractId);
            Assert.Empty((await client.GetFromJsonAsync<ContractPage>($"/api/contracts?customerId={customer.CustomerId}&status=Active"))!.Items);
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/contracts/9223372036854775807")).StatusCode);
            // Inject a failure after the aggregate was flushed but before schedule save.
            await using(var failing=new ImsDbContext(new DbContextOptionsBuilder<ImsDbContext>().UseNpgsql(connection).AddInterceptors(new FailScheduleSave()).Options))
                await Assert.ThrowsAsync<InvalidOperationException>(()=>new ContractService(failing).CreateAsync(Request("rollback"),auth.User.Id,default));
            Assert.False(await db.Contracts.AnyAsync(x=>x.ContractNumber==key+"rollback"));
            Assert.False(await db.Guarantors.AnyAsync(x=>x.IdentificationNumber==key+"rollback"));
            Assert.Equal(2,await db.Contracts.CountAsync(x=>x.CustomerId==customer.CustomerId));
            // Historical item prices stay locked after catalog changes.
            p1.CashPrice=999;await db.SaveChangesAsync();
            Assert.Equal(25m,(await client.GetFromJsonAsync<ContractDetails>(created.Headers.Location))!.Items[0].UnitPrice);

            var before=await client.GetFromJsonAsync<ContractDetails>(created.Headers.Location);
            var activated=await client.PostAsJsonAsync($"/api/contracts/{id}/activate",new ActivateContractRequest { DownPaymentConfirmed=true,Reference=key,Note="Live activation" });
            Assert.Equal(HttpStatusCode.OK,activated.StatusCode);
            var after=(await activated.Content.ReadFromJsonAsync<ContractDetails>())!;
            Assert.Equal("Active",after.Contract.Status);
            Assert.Equal(before!.Contract with { Status="Active" },after.Contract);
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Items),System.Text.Json.JsonSerializer.Serialize(after.Items));
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Guarantors),System.Text.Json.JsonSerializer.Serialize(after.Guarantors));
            Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Installments),System.Text.Json.JsonSerializer.Serialize(after.Installments));
            var audit=Assert.Single(await db.AuditLogs.AsNoTracking().Where(x=>x.TargetEntityId==id&&x.ActionType=="ContractActivated").ToListAsync());
            Assert.Equal(auth.User.Id,audit.UserId);Assert.Equal("Contract",audit.TargetEntityType);Assert.Equal("Draft",audit.PreviousState);Assert.Equal("Active",audit.NewState);
            Assert.Equal(key,audit.Reference);Assert.Equal("Live activation",audit.Note);Assert.Equal(DateTimeKind.Utc,audit.Timestamp.Kind);
            Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync($"/api/contracts/{id}/activate",new ActivateContractRequest { DownPaymentConfirmed=true })).StatusCode);
            Assert.Single(await db.AuditLogs.AsNoTracking().Where(x=>x.TargetEntityId==id&&x.ActionType=="ContractActivated").ToListAsync());

            var raceCreated=await client.PostAsJsonAsync("/api/contracts",Request("race",guarantorId:details.Guarantors[0].GuarantorId));
            Assert.Equal(HttpStatusCode.Created,raceCreated.StatusCode);
            var raceId=(await raceCreated.Content.ReadFromJsonAsync<ContractDetails>())!.Contract.ContractId;
            var attempts=await Task.WhenAll(
                client.PostAsJsonAsync($"/api/contracts/{raceId}/activate",new ActivateContractRequest { DownPaymentConfirmed=true }),
                client.PostAsJsonAsync($"/api/contracts/{raceId}/activate",new ActivateContractRequest { DownPaymentConfirmed=true }));
            Assert.Single(attempts,x=>x.StatusCode==HttpStatusCode.OK);
            Assert.Single(attempts,x=>x.StatusCode==HttpStatusCode.Conflict);
            Assert.Single(await db.AuditLogs.AsNoTracking().Where(x=>x.TargetEntityId==raceId&&x.ActionType=="ContractActivated").ToListAsync());
        }
        finally
        {
            var contractIds=await db.Contracts.Where(x=>x.CustomerId==customer.CustomerId).Select(x=>x.ContractId).ToListAsync();
            await db.AuditLogs.Where(x=>x.TargetEntityType=="Contract"&&contractIds.Contains(x.TargetEntityId)).ExecuteDeleteAsync();
            await db.Contracts.Where(x=>x.CustomerId==customer.CustomerId).ExecuteDeleteAsync();
            await db.Guarantors.Where(x=>x.IdentificationNumber.StartsWith(key)).ExecuteDeleteAsync();
            await db.Products.Where(x=>x.ProductId==p1.ProductId||x.ProductId==p2.ProductId).ExecuteDeleteAsync();
            await db.Customers.Where(x=>x.CustomerId==customer.CustomerId).ExecuteDeleteAsync();
        }
    }
    private sealed class FailScheduleSave : SaveChangesInterceptor
    {
        private int calls;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,InterceptionResult<int> result,CancellationToken cancellationToken=default)
        { if(++calls==2) throw new InvalidOperationException("Injected schedule failure");return ValueTask.FromResult(result); }
    }
}

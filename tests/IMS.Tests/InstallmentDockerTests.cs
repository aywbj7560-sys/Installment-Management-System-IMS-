using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IMS.Application.Authentication;
using IMS.Application.Installments;
using IMS.Application.Payments;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IMS.Tests;

[CollectionDefinition("Installments PostgreSQL", DisableParallelization = true)]
public sealed class InstallmentPostgresCollection { }

[Collection("Installments PostgreSQL")]
public class InstallmentDockerTests
{
    [LiveCustomerFact]
    public async Task PaymentBalancesCollectionOrderAndNoMutations()
    {
        using var client = new HttpClient { BaseAddress = new Uri("http://api:8080") };
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = Environment.GetEnvironmentVariable("SeedAdmin__Email")!, Password = Environment.GetEnvironmentVariable("SeedAdmin__Password")!
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        await using var db = new ImsDbContext(new DbContextOptionsBuilder<ImsDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")).Options);
        // Use the API's captured UTC date, not the host machine's local timezone.
        var today = (await client.GetFromJsonAsync<InstallmentPage>("/api/installments?pageSize=1"))!.AsOfUtcDate;
        var key = "ITEST-" + Guid.NewGuid().ToString("N");
        var customer = new Customer { FullName = key, IdentificationNumber = key, Phone = "07700000000", Address = "test" };
        Contract Fixture(string suffix, ContractStatus status = ContractStatus.Active) => new()
        {
            ContractNumber = key + suffix, Customer = customer, CreatedByUserId = auth.User.Id, ContractDate = DateTime.UtcNow,
            Status = status, TotalAmount = 120, RemainingAmount = 120,
            Installments = Enumerable.Range(1, 12).Select(i => new Installment
            {
                InstallmentNumber = i, Amount = 10, RemainingAmount = 10, Status = InstallmentStatus.Pending,
                DueDate = today.AddDays(i <= 3 ? i - 3 : i * 30)
            }).ToList()
        };
        var main = Fixture(""); var tie = Fixture("tie");
        foreach (var row in tie.Installments) row.DueDate = row.InstallmentNumber == 1 ? today.AddDays(-1) : today.AddDays(100 + row.InstallmentNumber);
        var inactive = new[] { ContractStatus.Draft, ContractStatus.Voided, ContractStatus.Completed, ContractStatus.Defaulted }
            .Select((status, i) => Fixture(i.ToString(), status)).ToArray();
        db.AddRange(main, tie); db.AddRange(inactive); await db.SaveChangesAsync();
        var ids = main.Installments.OrderBy(x => x.InstallmentNumber).Select(x => x.InstallmentId).ToArray();
        async Task<string> Snapshot() => JsonSerializer.Serialize(new
        {
            Contracts = await db.Contracts.AsNoTracking().Where(x => x.CustomerId == customer.CustomerId).OrderBy(x => x.ContractId)
                .Select(x => new { x.ContractId, x.TotalAmount, x.DownPayment, x.RemainingAmount, x.Status }).ToListAsync(),
            Installments = await db.Installments.AsNoTracking().Where(x => x.Contract.CustomerId == customer.CustomerId).OrderBy(x => x.InstallmentId)
                .Select(x => new { x.InstallmentId, x.ContractId, x.InstallmentNumber, x.DueDate, x.Amount, x.PaidAmount, x.RemainingAmount, x.Status }).ToListAsync(),
            Payments = await db.Payments.AsNoTracking().Where(x => x.Contract.CustomerId == customer.CustomerId).OrderBy(x => x.PaymentId)
                .Select(x => new { x.PaymentId, x.PaymentReference, x.ContractId, x.ReceivedByUserId, x.PaymentDate, x.Amount, x.PaymentMethod, x.Notes }).ToListAsync(),
            Allocations = await db.PaymentAllocations.AsNoTracking().Where(x => x.Payment.Contract.CustomerId == customer.CustomerId).OrderBy(x => x.PaymentAllocationId)
                .Select(x => new { x.PaymentAllocationId, x.PaymentId, x.InstallmentId, x.AllocatedAmount, x.CreatedAt }).ToListAsync()
        });
        try
        {
            var payment = await client.PostAsJsonAsync("/api/payments", new PaymentRequest
                { ContractId = main.ContractId, PaymentReference = key, Amount = 15, PaymentMethod = "Cash" });
            Assert.Equal(HttpStatusCode.Created, payment.StatusCode);
            var receipt = (await payment.Content.ReadFromJsonAsync<PaymentDetails>())!;
            Assert.Equal(2, receipt.Allocations.Count);
            var before = await Snapshot();
            var schedule = (await client.GetFromJsonAsync<ContractSchedule>($"/api/contracts/{main.ContractId}/installments"))!;
            Assert.Equal(12, schedule.Items.Count); Assert.Equal(Enumerable.Range(1, 12), schedule.Items.Select(x => x.InstallmentNumber));
            Assert.Equal("Paid", schedule.Items[0].Status); Assert.Equal(10m, schedule.Items[0].PaidAmount); Assert.Equal(0m, schedule.Items[0].RemainingAmount);
            Assert.Equal("Partially Paid", schedule.Items[1].Status); Assert.Equal(5m, schedule.Items[1].PaidAmount); Assert.Equal(5m, schedule.Items[1].RemainingAmount);
            Assert.True(schedule.Items[1].IsPastDue); Assert.True(schedule.Items[1].IsOpen);
            Assert.False(schedule.Items[2].IsPastDue); Assert.Equal("Pending", schedule.Items[2].Status);
            Assert.All(schedule.Items.Skip(3), x => { Assert.False(x.IsPastDue); Assert.Equal("Pending", x.Status); Assert.Equal(0m, x.PaidAmount); Assert.Equal(10m, x.RemainingAmount); });
            var detail = (await client.GetFromJsonAsync<InstallmentDetails>($"/api/installments/{ids[1]}"))!.Installment;
            Assert.Equal(schedule.Items[1], detail); Assert.Equal(customer.CustomerId, detail.Customer.CustomerId);
            Assert.Equal(main.ContractNumber, detail.Contract.ContractNumber);
            var page = (await client.GetFromJsonAsync<InstallmentPage>($"/api/installments?contractId={main.ContractId}&pageSize=5"))!;
            var page2 = (await client.GetFromJsonAsync<InstallmentPage>($"/api/installments?contractId={main.ContractId}&pageSize=5&page=2"))!;
            Assert.Equal(12, page.TotalCount); Assert.Equal(5, page.Items.Count);
            Assert.Empty(page.Items.Select(x => x.InstallmentId).Intersect(page2.Items.Select(x => x.InstallmentId)));
            var partial = (await client.GetFromJsonAsync<InstallmentPage>($"/api/installments?customerId={customer.CustomerId}&status=Partially%20Paid"))!;
            Assert.Equal(ids[1], Assert.Single(partial.Items).InstallmentId);
            var past = (await client.GetFromJsonAsync<InstallmentPage>($"/api/installments?contractId={main.ContractId}&pastDueOnly=true"))!;
            Assert.Equal(ids[1], Assert.Single(past.Items).InstallmentId);
            Assert.Equal(11, (await client.GetFromJsonAsync<InstallmentPage>($"/api/installments?contractId={main.ContractId}&openOnly=true"))!.TotalCount);
            var due = (await client.GetFromJsonAsync<InstallmentPage>($"/api/installments?contractId={main.ContractId}&dueFrom={today:yyyy-MM-dd}&dueTo={today:yyyy-MM-dd}"))!;
            Assert.Equal(ids[2], Assert.Single(due.Items).InstallmentId);
            Assert.Equal(72, (await client.GetFromJsonAsync<InstallmentPage>($"/api/installments?search={key.ToLowerInvariant()}"))!.TotalCount);
            Assert.Empty((await client.GetFromJsonAsync<InstallmentPage>($"/api/installments?search={key}%25"))!.Items);
            var queue = (await client.GetFromJsonAsync<InstallmentPage>($"/api/collections/due?customerId={customer.CustomerId}"))!;
            Assert.Equal(new[] { tie.Installments.First().InstallmentId, ids[1], ids[2] }, queue.Items.Select(x => x.InstallmentId));
            Assert.All(queue.Items, x => { Assert.True(x.IsOpen); Assert.Equal("Active", x.Contract.Status); });
            Assert.Equal(2, (await client.GetFromJsonAsync<InstallmentPage>($"/api/collections/due?customerId={customer.CustomerId}&pastDueOnly=true"))!.TotalCount);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/installments/9223372036854775807")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/contracts/9223372036854775807/installments")).StatusCode);
            Assert.Equal(before, await Snapshot());
            var history = (await client.GetFromJsonAsync<PaymentDetails>(payment.Headers.Location))!;
            Assert.Equal(receipt.Allocations, history.Allocations); Assert.Equal(15m, history.Allocations.Sum(x => x.AllocatedAmount));
        }
        finally
        {
            await db.PaymentAllocations.Where(x => x.Payment.Contract.CustomerId == customer.CustomerId).ExecuteDeleteAsync();
            await db.Payments.Where(x => x.Contract.CustomerId == customer.CustomerId).ExecuteDeleteAsync();
            await db.Contracts.Where(x => x.CustomerId == customer.CustomerId).ExecuteDeleteAsync();
            await db.Customers.Where(x => x.CustomerId == customer.CustomerId).ExecuteDeleteAsync();
        }
    }
}

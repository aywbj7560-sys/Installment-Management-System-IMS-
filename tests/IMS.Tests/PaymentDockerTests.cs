using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IMS.Application.Authentication;
using IMS.Application.Payments;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Payments;
using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IMS.Tests;

[CollectionDefinition("Payments PostgreSQL", DisableParallelization = true)]
public sealed class PaymentPostgresCollection { }

[Collection("Payments PostgreSQL")]
public class PaymentDockerTests
{
    [LiveCustomerFact]
    public async Task PaymentsPersistReconcileRollbackAndSerialize()
    {
        using var client = new HttpClient { BaseAddress = new Uri("http://api:8080") };
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = Environment.GetEnvironmentVariable("SeedAdmin__Email")!,
            Password = Environment.GetEnvironmentVariable("SeedAdmin__Password")!
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        await using var db = new ImsDbContext(new DbContextOptionsBuilder<ImsDbContext>().UseNpgsql(connection).Options);
        var key = "PTEST-" + Guid.NewGuid().ToString("N");
        var customer = new Customer { FullName = key, IdentificationNumber = key, Phone = "0", Address = "test" };
        // Active fixtures are intentional: activation is outside the current API scope.
        Contract Fixture(string suffix) => new()
        {
            ContractNumber = key + suffix, Customer = customer, CreatedByUserId = auth.User.Id,
            ContractDate = DateTime.UtcNow, TotalAmount = 120, RemainingAmount = 120, Status = ContractStatus.Active,
            Installments = Enumerable.Range(1, 12).Select(i => new Installment
            {
                InstallmentNumber = i, DueDate = new DateOnly(2028, 1, 1).AddMonths(i), Amount = 10,
                RemainingAmount = 10, Status = i == 1 ? InstallmentStatus.Overdue : InstallmentStatus.Pending
            }).ToList()
        };
        var contract = Fixture(""); var race = Fixture("race"); var other = Fixture("other");
        db.AddRange(contract, race, other); await db.SaveChangesAsync();
        PaymentRequest Request(decimal amount, string suffix, long? id = null) => new()
        {
            ContractId = id ?? contract.ContractId, PaymentReference = key + suffix, Amount = amount,
            PaymentMethod = "Cash", PaymentDate = new DateTimeOffset(2028, 2, 1, 3, 0, 0, TimeSpan.FromHours(3)), Notes = "test receipt"
        };
        async Task<PaymentDetails> Pay(decimal amount, string suffix)
        {
            var response = await client.PostAsJsonAsync("/api/payments", Request(amount, suffix));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var details = (await response.Content.ReadFromJsonAsync<PaymentDetails>())!;
            Assert.Equal(amount, details.Allocations.Sum(x => x.AllocatedAmount));
            Assert.Equal(auth.User.Id, details.Payment.ReceivedByUserId);
            Assert.Equal(new DateTime(2028, 2, 1, 0, 0, 0, DateTimeKind.Utc), details.Payment.PaymentDate);
            Assert.All(details.Allocations, a => {
                Assert.True(a.AllocatedAmount > 0); Assert.True(a.Installment.PaidAmount <= a.Installment.Amount);
                Assert.Equal(a.Installment.Amount, a.Installment.PaidAmount + a.Installment.RemainingAmount);
            });
            var persisted = (await client.GetFromJsonAsync<PaymentDetails>(response.Headers.Location))!;
            Assert.Equal(details.Payment, persisted.Payment);
            Assert.Equal(details.Allocations.Count, persisted.Allocations.Count);
            Assert.Equal(amount, await db.PaymentAllocations.Where(x => x.PaymentId == details.Payment.PaymentId).SumAsync(x => x.AllocatedAmount));
            return details;
        }
        try
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/payments", Request(1, "missing", long.MaxValue))).StatusCode);
            foreach (var amount in new[] { 0m, -1m, 0.001m, 10000000000000m })
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/payments", Request(amount, "invalid"))).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/payments", Request(120.01m, "over"))).StatusCode);
            Assert.False(await db.Payments.AnyAsync(x => x.ContractId == contract.ContractId));
            foreach (var status in new[] { ContractStatus.Draft, ContractStatus.Voided, ContractStatus.Completed, ContractStatus.Defaulted })
            {
                other.Status = status; await db.SaveChangesAsync();
                Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/payments", Request(1, "state", other.ContractId))).StatusCode);
            }
            other.Status = ContractStatus.Active; await db.SaveChangesAsync();

            other.RemainingAmount = 119; await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/payments", Request(1, "inconsistent", other.ContractId))).StatusCode);
            other.RemainingAmount = 0;
            foreach (var i in other.Installments) { i.PaidAmount = i.Amount; i.RemainingAmount = 0; i.Status = InstallmentStatus.Paid; }
            await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/payments", Request(1, "empty", other.ContractId))).StatusCode);
            other.RemainingAmount = 120;
            foreach (var i in other.Installments) { i.PaidAmount = 0; i.RemainingAmount = i.Amount; i.Status = InstallmentStatus.Waived; }
            await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/payments", Request(1, "waived", other.ContractId))).StatusCode);
            foreach (var i in other.Installments) i.Status = InstallmentStatus.Pending;
            await db.SaveChangesAsync();

            var partial = await Pay(4.25m, "partial");
            var first = Assert.Single(partial.Allocations).Installment;
            Assert.Equal(1, first.InstallmentNumber); Assert.Equal("Partially Paid", first.Status);
            Assert.Equal(4.25m, first.PaidAmount); Assert.Equal(5.75m, first.RemainingAmount);
            Assert.Equal(115.75m, partial.Contract.RemainingAmount);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/payments", Request(1, "partial"))).StatusCode);
            var exact = await Pay(5.75m, "exact");
            Assert.Equal("Paid", Assert.Single(exact.Allocations).Installment.Status);
            var spanning = await Pay(25m, "span");
            Assert.Equal(new[] { 2, 3, 4 }, spanning.Allocations.Select(x => x.Installment.InstallmentNumber));
            Assert.Equal(new[] { 10m, 10m, 5m }, spanning.Allocations.Select(x => x.AllocatedAmount));
            Assert.Equal(new[] { "Paid", "Paid", "Partially Paid" }, spanning.Allocations.Select(x => x.Installment.Status));
            Assert.Equal(85m, spanning.Contract.RemainingAmount);
            Assert.Equal(InstallmentStatus.Pending, (await db.Installments.AsNoTracking().SingleAsync(x => x.ContractId == contract.ContractId && x.InstallmentNumber == 5)).Status);

            // Fail after allocations and balance updates have reached PostgreSQL, before commit.
            await using (var failing = new ImsDbContext(new DbContextOptionsBuilder<ImsDbContext>().UseNpgsql(connection)
                .AddInterceptors(new FailAfterAllocationSave()).Options))
                await Assert.ThrowsAsync<InvalidOperationException>(() => new PaymentService(failing).CreateAsync(Request(12m, "rollback"), auth.User.Id, default));
            Assert.False(await db.Payments.AnyAsync(x => x.PaymentReference == key + "rollback"));
            Assert.Equal(3, await db.Payments.CountAsync(x => x.ContractId == contract.ContractId));
            Assert.Equal(5, await db.PaymentAllocations.CountAsync(x => x.Payment.ContractId == contract.ContractId));
            Assert.Equal(85m, (await db.Contracts.AsNoTracking().SingleAsync(x => x.ContractId == contract.ContractId)).RemainingAmount);
            Assert.Equal(35m, await db.Installments.Where(x => x.ContractId == contract.ContractId).SumAsync(x => x.PaidAmount));

            var page = (await client.GetFromJsonAsync<PaymentPage>($"/api/payments?contractId={contract.ContractId}&customerId={customer.CustomerId}&search={key.ToLowerInvariant()}&pageSize=2"))!;
            Assert.Equal(3, page.TotalCount); Assert.Equal(2, page.Items.Count);
            var page2 = (await client.GetFromJsonAsync<PaymentPage>($"/api/payments?contractId={contract.ContractId}&pageSize=2&page=2"))!;
            Assert.Single(page2.Items); Assert.DoesNotContain(page2.Items[0].PaymentId, page.Items.Select(x => x.PaymentId));
            Assert.Equal(3, (await client.GetFromJsonAsync<PaymentPage>($"/api/contracts/{contract.ContractId}/payments"))!.TotalCount);
            Assert.Empty((await client.GetFromJsonAsync<PaymentPage>($"/api/contracts/{other.ContractId}/payments"))!.Items);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/contracts/9223372036854775807/payments")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/payments/9223372036854775807")).StatusCode);
            Assert.Empty((await client.GetFromJsonAsync<PaymentPage>($"/api/payments?contractId={other.ContractId}"))!.Items);

            var full = await Pay(85m, "full"); Assert.Equal("Completed", full.Contract.Status); Assert.Equal(0m, full.Contract.RemainingAmount);
            var schedule = await db.Installments.AsNoTracking().Where(x => x.ContractId == contract.ContractId).ToListAsync();
            Assert.All(schedule, x => { Assert.Equal(InstallmentStatus.Paid, x.Status); Assert.Equal(0m, x.RemainingAmount); Assert.Equal(x.Amount, x.PaidAmount); });
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/payments", Request(0.01m, "closed"))).StatusCode);

            // Competing receipts cannot both spend the same outstanding balance.
            var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/payments", Request(80, "race1", race.ContractId)),
                client.PostAsJsonAsync("/api/payments", Request(80, "race2", race.ContractId)));
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(40m, (await db.Contracts.AsNoTracking().SingleAsync(x => x.ContractId == race.ContractId)).RemainingAmount);
            Assert.Equal(80m, await db.PaymentAllocations.Where(x => x.Payment.ContractId == race.ContractId).SumAsync(x => x.AllocatedAmount));
            Assert.False(await db.PaymentAllocations.AnyAsync(x => x.Payment.ContractId != x.Installment.ContractId));

            // Dates determine priority before sequence; equal dates use installment number.
            var ordered = other.Installments.OrderBy(x => x.InstallmentNumber).ToArray();
            ordered[1].DueDate = ordered[2].DueDate = new DateOnly(2027, 1, 1);
            await db.SaveChangesAsync();
            var priority = await client.PostAsJsonAsync("/api/payments", Request(15, "priority", other.ContractId));
            Assert.Equal(HttpStatusCode.Created, priority.StatusCode);
            Assert.Equal(new[] { 2, 3 }, (await priority.Content.ReadFromJsonAsync<PaymentDetails>())!.Allocations.Select(x => x.Installment.InstallmentNumber));
        }
        finally
        {
            await db.PaymentAllocations.Where(x => x.Payment.Contract.CustomerId == customer.CustomerId).ExecuteDeleteAsync();
            await db.Payments.Where(x => x.Contract.CustomerId == customer.CustomerId).ExecuteDeleteAsync();
            await db.Contracts.Where(x => x.CustomerId == customer.CustomerId).ExecuteDeleteAsync();
            await db.Customers.Where(x => x.CustomerId == customer.CustomerId).ExecuteDeleteAsync();
        }
    }

    private sealed class FailAfterAllocationSave : SaveChangesInterceptor
    {
        private int calls;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (++calls == 2) throw new InvalidOperationException("Forced failure after allocation persistence.");
            return ValueTask.FromResult(result);
        }
    }
}

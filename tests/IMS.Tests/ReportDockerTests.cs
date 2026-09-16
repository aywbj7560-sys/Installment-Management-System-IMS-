using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IMS.Application.Authentication;
using IMS.Application.Contracts;
using IMS.Application.Customers;
using IMS.Application.Guarantors;
using IMS.Application.Installments;
using IMS.Application.Payments;
using IMS.Application.Products;
using IMS.Application.Reports;
using IMS.Domain.Enums;
using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IMS.Tests;

[CollectionDefinition("Reports PostgreSQL", DisableParallelization = true)]
public sealed class ReportPostgresCollection { }

[Collection("Reports PostgreSQL")]
public class ReportDockerTests
{
    [LiveCustomerFact]
    public async Task ReportsReconcileWithPostgresAndNeverWrite()
    {
        using var client = new HttpClient { BaseAddress = new Uri("http://api:8080") };
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        { Email = Environment.GetEnvironmentVariable("SeedAdmin__Email")!, Password = Environment.GetEnvironmentVariable("SeedAdmin__Password")! });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        await using var db = new ImsDbContext(new DbContextOptionsBuilder<ImsDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")).Options);
        var key = "RTEST-" + Guid.NewGuid().ToString("N");
        long customerId = 0, productId = 0, guarantorId = 0;
        async Task<T> Create<T>(string path, object request)
        {
            var response = await client.PostAsJsonAsync(path, request); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<T>())!;
        }
        async Task<string> Snapshot() => JsonSerializer.Serialize(new
        {
            Contracts = await db.Contracts.AsNoTracking().Where(x => x.CustomerId == customerId).OrderBy(x => x.ContractId).ToListAsync(),
            Installments = await db.Installments.AsNoTracking().Where(x => x.Contract.CustomerId == customerId).OrderBy(x => x.InstallmentId).ToListAsync(),
            Payments = await db.Payments.AsNoTracking().Where(x => x.Contract.CustomerId == customerId).OrderBy(x => x.PaymentId).ToListAsync(),
            Allocations = await db.PaymentAllocations.AsNoTracking().Where(x => x.Payment.Contract.CustomerId == customerId).OrderBy(x => x.PaymentAllocationId).ToListAsync(),
            Links = await db.ContractGuarantors.AsNoTracking().Where(x => x.Contract.CustomerId == customerId).OrderBy(x => x.ContractGuarantorId).ToListAsync()
        });
        try
        {
            customerId = (await Create<CustomerResponse>("/api/customers", new CustomerRequest { FullName = key, IdentificationNumber = key, Phone = "07700000000", Address = "test" })).CustomerId;
            productId = (await Create<ProductResponse>("/api/products", new ProductRequest { ProductCode = key, Name = key, CashPrice = 120 })).ProductId;
            guarantorId = (await Create<GuarantorResponse>("/api/guarantors", new GuarantorRequest { FullName = key, IdentificationNumber = key, Phone = "07700000000", Address = "test" })).GuarantorId;
            ContractRequest Request(string suffix) => new() { ContractNumber = key + suffix, CustomerId = customerId,
                ContractDate = new DateTimeOffset(2028, 6, 15, 0, 0, 0, TimeSpan.Zero), Items = [new(productId, 1, 120)], GuarantorId = guarantorId, GuaranteeNotes = "Historical note" };
            var first = await Create<ContractDetails>("/api/contracts", Request(""));
            await Create<ContractDetails>("/api/contracts", Request("draft"));
            var id = first.Contract.ContractId;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            // Activation is not an API operation yet. Only fixture setup changes status/dates.
            var contract = await db.Contracts.Include(x => x.Installments).SingleAsync(x => x.ContractId == id);
            contract.Status = ContractStatus.Active;
            foreach (var i in contract.Installments) i.DueDate = today.AddDays(i.InstallmentNumber - 3);
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var p1 = await Create<PaymentDetails>("/api/payments", new PaymentRequest { ContractId = id, PaymentReference = key + "1", PaymentMethod = "Cash", Amount = 15,
                PaymentDate = new DateTimeOffset(2028, 6, 15, 23, 59, 59, TimeSpan.Zero) });
            await Create<PaymentDetails>("/api/payments", new PaymentRequest { ContractId = id, PaymentReference = key + "2", PaymentMethod = "Bank Transfer", Amount = 1,
                PaymentDate = new DateTimeOffset(2028, 6, 16, 0, 0, 0, TimeSpan.Zero) });
            var before = await Snapshot();

            var summary = (await client.GetFromJsonAsync<ReportSummary>("/api/reports/summary"))!;
            Assert.Equal(await db.Customers.LongCountAsync(), summary.TotalCustomers);
            Assert.Equal(await db.Customers.LongCountAsync(x => x.Status == CustomerStatus.Active), summary.ActiveCustomers);
            Assert.Equal(await db.Products.LongCountAsync(), summary.TotalProducts);
            Assert.Equal(await db.Products.LongCountAsync(x => x.IsActive), summary.ActiveProducts);
            Assert.Equal(await db.Contracts.LongCountAsync(), summary.TotalContracts);
            foreach (var status in Enum.GetValues<ContractStatus>()) Assert.Equal(await db.Contracts.LongCountAsync(x => x.Status == status), summary.ContractsByStatus[status.ToString()]);
            Assert.Equal(await db.Contracts.SumAsync(x => x.TotalAmount), summary.TotalContractValue);
            Assert.Equal(await db.Contracts.SumAsync(x => x.TotalAmount - x.DownPayment), summary.TotalFinancedPrincipal);
            Assert.Equal(await db.Contracts.SumAsync(x => x.RemainingAmount), summary.TotalContractRemaining);
            Assert.Equal(await db.Contracts.Where(x => x.Status == ContractStatus.Active).SumAsync(x => x.RemainingAmount), summary.ActiveContractRemaining);
            Assert.Equal(await db.Payments.SumAsync(x => x.Amount), summary.TotalPaymentsReceived);
            var open = db.Installments.Where(x => x.RemainingAmount > 0 && x.Status != InstallmentStatus.Paid && x.Status != InstallmentStatus.Waived);
            Assert.Equal(await open.LongCountAsync(), summary.OpenInstallmentCount); Assert.Equal(await open.SumAsync(x => x.RemainingAmount), summary.OpenInstallmentBalance);
            var past = db.Installments.Where(x => x.DueDate < summary.AsOfUtcDate && x.RemainingAmount > 0);
            Assert.Equal(await past.LongCountAsync(), summary.PastDueInstallmentCount); Assert.Equal(await past.SumAsync(x => x.RemainingAmount), summary.PastDueInstallmentBalance);

            var contracts = (await client.GetFromJsonAsync<ReportPage<ContractReportRow>>($"/api/reports/contracts?customerId={customerId}&pageSize=1"))!;
            Assert.Equal(2, contracts.TotalCount); Assert.Single(contracts.Items);
            var second = (await client.GetFromJsonAsync<ReportPage<ContractReportRow>>($"/api/reports/contracts?customerId={customerId}&pageSize=1&page=2"))!;
            Assert.NotEqual(contracts.Items[0].ContractId, Assert.Single(second.Items).ContractId);
            var filtered = (await client.GetFromJsonAsync<ReportPage<ContractReportRow>>($"/api/reports/contracts?status=Active&search={key.ToLowerInvariant()}&dateFrom=2028-06-15&dateTo=2028-06-15"))!;
            Assert.Equal(id, Assert.Single(filtered.Items).ContractId); Assert.Equal(1, filtered.Items[0].PaidInstallmentCount); Assert.Equal(11, filtered.Items[0].OpenInstallmentCount);
            var payments = (await client.GetFromJsonAsync<ReportPage<PaymentReportRow>>($"/api/reports/payments?contractId={id}&customerId={customerId}&pageSize=1"))!;
            Assert.Equal(2, payments.TotalCount); Assert.Equal(15m, Assert.Single(payments.Items).Amount); Assert.Equal(auth.User.Id, payments.Items[0].ReceivedByUserId);
            var paymentPage2 = (await client.GetFromJsonAsync<ReportPage<PaymentReportRow>>($"/api/reports/payments?contractId={id}&pageSize=1&page=2"))!;
            Assert.Equal(1m, Assert.Single(paymentPage2.Items).Amount);
            var dated = (await client.GetFromJsonAsync<ReportPage<PaymentReportRow>>($"/api/reports/payments?contractId={id}&dateFrom=2028-06-15&dateTo=2028-06-15&paymentMethod=Cash&search={key}"))!;
            Assert.Equal(p1.Payment.PaymentId, Assert.Single(dated.Items).PaymentId); Assert.Equal(15m, dated.Items[0].AllocatedTotal);
            var outstanding = (await client.GetFromJsonAsync<ReportPage<InstallmentView>>($"/api/reports/outstanding?customerId={customerId}"))!;
            Assert.Equal(11, outstanding.TotalCount); Assert.Equal(104m, outstanding.Items.Sum(x => x.RemainingAmount));
            Assert.Equal("Partially Paid", outstanding.Items[0].Status); Assert.True(outstanding.Items[0].IsPastDue);
            Assert.All(outstanding.Items.Skip(1), x => Assert.False(x.IsPastDue));
            Assert.Single((await client.GetFromJsonAsync<ReportPage<InstallmentView>>($"/api/reports/outstanding?contractId={id}&pastDueOnly=true&status=PartiallyPaid"))!.Items);
            Assert.Single((await client.GetFromJsonAsync<ReportPage<InstallmentView>>($"/api/reports/outstanding?contractId={id}&dueFrom={today:yyyy-MM-dd}&dueTo={today:yyyy-MM-dd}"))!.Items);

            var statement = (await client.GetFromJsonAsync<ContractStatement>($"/api/reports/contracts/{id}/statement?pageSize=1"))!;
            Assert.Equal(productId, Assert.Single(statement.Items.Items).ProductId); Assert.Equal(guarantorId, Assert.Single(statement.Guarantors.Items).GuarantorId);
            Assert.Equal("Historical note", statement.Guarantors.Items[0].GuaranteeNotes); Assert.Equal(12, statement.Installments.Count);
            Assert.Equal(2, statement.Payments.TotalCount); Assert.Single(statement.Payments.Items);
            Assert.Equal(new StatementTotals(104m, 104m, 16m, 16m, 16m), statement.Totals);
            var customer = (await client.GetFromJsonAsync<CustomerStatement>($"/api/reports/customers/{customerId}/statement?pageSize=1"))!;
            Assert.Equal(2, customer.Contracts.TotalCount); Assert.Equal(24, customer.Installments.TotalCount); Assert.Equal(2, customer.Payments.TotalCount);
            Assert.Equal(new StatementTotals(224m, 224m, 16m, 16m, 16m), customer.Totals);
            var next = (await client.GetFromJsonAsync<CustomerStatement>($"/api/reports/customers/{customerId}/statement?pageSize=1&contractsPage=2&installmentsPage=2&paymentsPage=2"))!;
            Assert.NotEqual(customer.Contracts.Items[0].ContractId, next.Contracts.Items[0].ContractId);
            Assert.NotEqual(customer.Installments.Items[0].InstallmentId, next.Installments.Items[0].InstallmentId);
            Assert.NotEqual(customer.Payments.Items[0].PaymentId, next.Payments.Items[0].PaymentId); Assert.Equal(customer.Totals, next.Totals);
            foreach (var kind in new[] { "customers", "contracts" })
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/reports/{kind}/9223372036854775807/statement")).StatusCode);
            Assert.Equal(before, await Snapshot());
        }
        finally
        {
            await db.PaymentAllocations.Where(x => x.Payment.Contract.CustomerId == customerId).ExecuteDeleteAsync();
            await db.Payments.Where(x => x.Contract.CustomerId == customerId).ExecuteDeleteAsync();
            await db.Contracts.Where(x => x.CustomerId == customerId).ExecuteDeleteAsync();
            await db.Guarantors.Where(x => x.GuarantorId == guarantorId).ExecuteDeleteAsync();
            await db.Products.Where(x => x.ProductId == productId).ExecuteDeleteAsync();
            await db.Customers.Where(x => x.CustomerId == customerId).ExecuteDeleteAsync();
        }
    }
}

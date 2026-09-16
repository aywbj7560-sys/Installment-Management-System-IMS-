using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IMS.API.Extensions;
using IMS.Application.Authentication;
using IMS.Application.Installments;
using IMS.Application.Reports;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Authentication;
using IMS.Infrastructure.Persistence;
using IMS.Infrastructure.Reports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IMS.Tests;

public class ReportTests
{
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2028, 6, 15, 23, 59, 0, TimeSpan.Zero);
    }
    private static async Task<WebApplication> Host(bool empty = false)
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        { ["Jwt:Key"] = "report-test-signing-key-at-least-32-bytes-long", ["Jwt:Issuer"] = "tests", ["Jwt:Audience"] = "tests" });
        builder.Services.AddImsAuthentication(builder.Configuration);
        var database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<ImsDbContext>(o => o.UseInMemoryDatabase(database));
        builder.Services.AddSingleton<TimeProvider>(new FixedClock()); builder.Services.AddScoped<IReportService, ReportService>();
        var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapReportEndpoints();
        if (!empty)
        {
            await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ImsDbContext>();
            var user = new User { UserId = 1, FullName = "Officer", Role = new Role { RoleName = RoleNames.Admin } };
            var customer = new Customer { CustomerId = 1, FullName = "Alpha Customer", Phone = "0" };
            var product = new Product { ProductId = 1, ProductCode = "P-1", Name = "Product", CashPrice = 120 };
            var contract = new Contract { ContractId = 1, ContractNumber = "CNT-ALPHA", Customer = customer, CreatedByUser = user,
                ContractDate = new DateTime(2028, 6, 15, 0, 0, 0, DateTimeKind.Utc), TotalAmount = 120, RemainingAmount = 105, Status = ContractStatus.Active,
                ContractItems = [new ContractItem { Product = product, Quantity = 1, UnitPrice = 120, Subtotal = 120 }],
                ContractGuarantors = [new ContractGuarantor { Guarantor = new Guarantor { FullName = "Guarantor", Phone = "0" }, Notes = "Guarantee" }] };
            for (var i = 1; i <= 12; i++) contract.Installments.Add(new Installment { InstallmentId = i, InstallmentNumber = i,
                DueDate = new DateOnly(2028, 6, 15).AddDays(i - 3), Amount = 10, PaidAmount = i == 1 ? 10 : i == 2 ? 5 : 0,
                RemainingAmount = i == 1 ? 0 : i == 2 ? 5 : 10, Status = i == 1 ? InstallmentStatus.Paid : i == 2 ? InstallmentStatus.PartiallyPaid : InstallmentStatus.Pending });
            var payment = new Payment { PaymentId = 1, PaymentReference = "REF-ONE", Contract = contract, ReceivedByUser = user,
                PaymentDate = contract.ContractDate, Amount = 15, PaymentMethod = "Cash",
                PaymentAllocations = [new PaymentAllocation { Installment = contract.Installments.First(), AllocatedAmount = 10 },
                    new PaymentAllocation { Installment = contract.Installments.Skip(1).First(), AllocatedAmount = 5 }] };
            var completed = new Contract { ContractId = 2, ContractNumber = "CNT-BETA", Customer = new Customer { CustomerId = 2, FullName = "Beta", Phone = "1", Status = CustomerStatus.Inactive },
                CreatedByUser = user, TotalAmount = 20, RemainingAmount = 0, Status = ContractStatus.Completed, ContractDate = contract.ContractDate.AddDays(1),
                Installments = [new Installment { InstallmentId = 13, InstallmentNumber = 1, Amount = 20, PaidAmount = 20, Status = InstallmentStatus.Paid, DueDate = new(2028, 6, 1) }] };
            var paid = new Payment { PaymentId = 2, PaymentReference = "REF-TWO", Contract = completed, ReceivedByUser = user, Amount = 20,
                PaymentMethod = "Bank Transfer", PaymentDate = completed.ContractDate,
                PaymentAllocations = [new PaymentAllocation { Installment = completed.Installments.First(), AllocatedAmount = 20 }] };
            db.AddRange(payment, paid, new Product { ProductId = 2, ProductCode = "P-2", Name = "Inactive", IsActive = false },
                new Contract { ContractId = 3, ContractNumber = "DRAFT", Customer = customer, CreatedByUser = user, TotalAmount = 12, RemainingAmount = 12, ContractDate = contract.ContractDate.AddDays(2) });
            await db.SaveChangesAsync();
        }
        await app.StartAsync(); return app;
    }
    private static HttpClient Client(WebApplication app, string? role = RoleNames.Admin)
    {
        var client = app.GetTestClient();
        if (role is not null)
        {
            var token = app.Services.GetRequiredService<JwtTokenGenerator>().Create(new User { UserId = 1, Role = new Role { RoleName = role } });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }
        return client;
    }

    [Theory]
    [InlineData(null, 401, 401)]
    [InlineData(RoleNames.Admin, 200, 200)]
    [InlineData(RoleNames.FinancialManager, 200, 200)]
    [InlineData(RoleNames.Auditor, 200, 200)]
    [InlineData(RoleNames.CollectionOfficer, 403, 200)]
    [InlineData(RoleNames.SalesAgent, 403, 403)]
    [InlineData("Unknown", 403, 403)]
    public async Task EveryRouteEnforcesRoles(string? role, int financial, int collection)
    {
        await using var app = await Host(); using var client = Client(app, role);
        foreach (var path in new[] { "summary", "contracts", "payments" }) Assert.Equal(financial, (int)(await client.GetAsync("/api/reports/" + path)).StatusCode);
        foreach (var path in new[] { "outstanding", "customers/1/statement", "contracts/1/statement" }) Assert.Equal(collection, (int)(await client.GetAsync("/api/reports/" + path)).StatusCode);
    }

    [Fact]
    public async Task EmptySummary()
    {
        await using var app = await Host(true); using var client = Client(app);
        var s = (await client.GetFromJsonAsync<ReportSummary>("/api/reports/summary"))!;
        Assert.Equal(0, s.TotalCustomers); Assert.Equal(0, s.TotalProducts); Assert.Equal(0, s.TotalContracts);
        Assert.Equal(0m, s.TotalPaymentsReceived); Assert.Equal(0m, s.TotalContractValue); Assert.Equal(0m, s.TotalFinancedPrincipal);
        Assert.Equal(0m, s.TotalContractRemaining); Assert.Equal(0m, s.OpenInstallmentBalance); Assert.Equal(0m, s.PastDueInstallmentBalance);
        Assert.All(s.ContractsByStatus, pair => Assert.Equal(0, pair.Value));
    }
    [Fact]
    public async Task SummaryAndStatementsReconcileWithoutRepairing()
    {
        await using var app = await Host(); using var client = Client(app);
        var s = (await client.GetFromJsonAsync<ReportSummary>("/api/reports/summary"))!;
        Assert.Equal(2, s.TotalCustomers); Assert.Equal(1, s.ActiveCustomers); Assert.Equal(2, s.TotalProducts); Assert.Equal(1, s.ActiveProducts);
        Assert.Equal(3, s.TotalContracts); Assert.Equal(1, s.CompletedContracts); Assert.Equal(1, s.ContractsByStatus["Draft"]); Assert.Equal(1, s.ContractsByStatus["Active"]);
        Assert.Equal(152m, s.TotalContractValue); Assert.Equal(152m, s.TotalFinancedPrincipal); Assert.Equal(117m, s.TotalContractRemaining);
        Assert.Equal(105m, s.ActiveContractRemaining); Assert.Equal(35m, s.TotalPaymentsReceived); Assert.Equal(11, s.OpenInstallmentCount);
        Assert.Equal(105m, s.OpenInstallmentBalance); Assert.Equal(1, s.PastDueInstallmentCount); Assert.Equal(5m, s.PastDueInstallmentBalance);
        var contract = (await client.GetFromJsonAsync<ContractStatement>("/api/reports/contracts/1/statement"))!;
        Assert.Single(contract.Items.Items); Assert.Equal("Guarantor", Assert.Single(contract.Guarantors.Items).FullName);
        Assert.Equal(12, contract.Installments.Count); Assert.Equal(105m, contract.Totals.ContractRemaining);
        Assert.Equal(contract.Totals.ContractRemaining, contract.Totals.InstallmentRemaining);
        Assert.Equal(15m, contract.Totals.PaymentsReceived); Assert.Equal(15m, contract.Totals.AllocationsTotal); Assert.Equal(15m, contract.Totals.InstallmentPaid);
        var customer = (await client.GetFromJsonAsync<CustomerStatement>("/api/reports/customers/1/statement?pageSize=1"))!;
        Assert.Equal(2, customer.Contracts.TotalCount); Assert.Single(customer.Contracts.Items); Assert.Single(customer.Installments.Items);
        Assert.Equal(117m, customer.Totals.ContractRemaining); Assert.Equal(105m, customer.Totals.InstallmentRemaining); // deliberately missing Draft schedule: no repair
        var next = (await client.GetFromJsonAsync<CustomerStatement>("/api/reports/customers/1/statement?pageSize=1&contractsPage=2&installmentsPage=2&paymentsPage=2"))!;
        Assert.Equal(3, Assert.Single(next.Contracts.Items).ContractId); Assert.Empty(next.Payments.Items); Assert.Equal(customer.Totals, next.Totals);
        var outPage = (await client.GetFromJsonAsync<ReportPage<InstallmentView>>("/api/reports/outstanding"))!;
        Assert.Equal(11, outPage.TotalCount); Assert.Equal("Partially Paid", outPage.Items[0].Status); Assert.True(outPage.Items[0].IsPastDue);
        Assert.All(outPage.Items.Skip(1), x => Assert.False(x.IsPastDue));
        Assert.Single((await client.GetFromJsonAsync<ReportPage<InstallmentView>>("/api/reports/outstanding?pastDueOnly=true"))!.Items);
        await using var scope = app.Services.CreateAsyncScope(); var service = scope.ServiceProvider.GetRequiredService<IReportService>();
        await service.SummaryAsync(default); await service.CustomerStatementAsync(1, new(), default); await service.ContractStatementAsync(1, new(), default);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<ImsDbContext>().ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("contracts", "status=Active", 1)]
    [InlineData("contracts", "customerId=1", 2)]
    [InlineData("contracts", "dateFrom=2028-06-15&dateTo=2028-06-15", 1)]
    [InlineData("contracts", "search=alpha", 2)]
    [InlineData("contracts", "search=customer", 2)]
    [InlineData("payments", "dateFrom=2028-06-15&dateTo=2028-06-15", 1)]
    [InlineData("payments", "contractId=2&customerId=2", 1)]
    [InlineData("payments", "paymentMethod=Bank%20Transfer", 1)]
    [InlineData("payments", "search=ref-one", 1)]
    [InlineData("payments", "search=cnt-alpha", 1)]
    [InlineData("payments", "search=customer", 1)]
    [InlineData("payments", "search=%25", 0)]
    public async Task FiltersAndPaging(string report, string query, int count)
    {
        await using var app = await Host(); using var client = Client(app);
        var json = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/reports/{report}?{query}&pageSize=1");
        Assert.Equal(count, json.GetProperty("totalCount").GetInt32()); Assert.Equal(Math.Min(count, 1), json.GetProperty("items").GetArrayLength());
        var page2 = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/reports/{report}?{query}&pageSize=1&page=2");
        Assert.Equal(Math.Min(Math.Max(count - 1, 0), 1), page2.GetProperty("items").GetArrayLength());
    }

    [Theory]
    [InlineData("contracts?page=0")]
    [InlineData("payments?pageSize=101")]
    [InlineData("payments?customerId=-1")]
    [InlineData("contracts?contractId=abc")]
    [InlineData("contracts?status=0")]
    [InlineData("payments?status=Paid")]
    [InlineData("contracts?paymentMethod=Cash")]
    [InlineData("payments?dateFrom=2028-02-30")]
    [InlineData("contracts?dateFrom=2028-06-16&dateTo=2028-06-15")]
    [InlineData("payments?search=%00")]
    [InlineData("outstanding?status=Paid")]
    [InlineData("outstanding?pastDueOnly=yes")]
    [InlineData("customers/0/statement")]
    [InlineData("contracts/1/statement?paymentsPage=0")]
    [InlineData("customers/1/statement?installmentsPage=2147483647")]
    public async Task InvalidQueries(string path)
    {
        await using var app = await Host(); using var client = Client(app);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/reports/" + path)).StatusCode);
    }
    [Fact]
    public async Task MissingStatementsAndNoWrites()
    {
        await using var app = await Host(); using var client = Client(app);
        foreach (var kind in new[] { "customers", "contracts" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/reports/{kind}/999/statement")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsJsonAsync("/api/reports/summary", new { })).StatusCode);
    }
}

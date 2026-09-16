using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IMS.API.Extensions;
using IMS.Application.Authentication;
using IMS.Application.Installments;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using IMS.Infrastructure.Authentication;
using IMS.Infrastructure.Installments;
using IMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IMS.Tests;

public class InstallmentTests
{
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2028, 6, 15, 23, 59, 0, TimeSpan.Zero);
    }

    private static async Task<WebApplication> Host()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "installment-test-signing-key-at-least-32-bytes", ["Jwt:Issuer"] = "tests", ["Jwt:Audience"] = "tests"
        });
        builder.Services.AddImsAuthentication(builder.Configuration);
        var database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<ImsDbContext>(o => o.UseInMemoryDatabase(database));
        builder.Services.AddSingleton<TimeProvider>(new FixedClock());
        builder.Services.AddScoped<IInstallmentService, InstallmentService>();
        var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapInstallmentEndpoints();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ImsDbContext>();
            var c = new Contract { ContractId = 1, ContractNumber = "CNT-ONE", Status = ContractStatus.Active,
                Customer = new Customer { CustomerId = 1, FullName = "Example Customer", Phone = "0770" } };
            for (var i = 1; i <= 6; i++) c.Installments.Add(new Installment
            {
                InstallmentId = i, InstallmentNumber = i, DueDate = new DateOnly(2028, 6, i == 4 ? 16 : i == 3 ? 15 : 10 + i % 3),
                Amount = 10, PaidAmount = i == 5 ? 10 : i == 2 ? 4 : 0, RemainingAmount = i == 5 ? 0 : i == 2 ? 6 : 10,
                Status = i == 5 ? InstallmentStatus.Paid : i == 6 ? InstallmentStatus.Waived : i == 2 ? InstallmentStatus.PartiallyPaid : InstallmentStatus.Pending
            });
            db.Add(c);
            db.Add(new Contract { ContractId = 2, ContractNumber = "DRAFT", Status = ContractStatus.Draft,
                Customer = new Customer { CustomerId = 2, FullName = "Other", Phone = "0" },
                Installments = [new Installment { InstallmentId = 7, InstallmentNumber = 1, DueDate = new(2028, 6, 1), Amount = 10, RemainingAmount = 10, Status = InstallmentStatus.Overdue }] });
            await db.SaveChangesAsync();
        }
        await app.StartAsync(); return app;
    }

    private static HttpClient Client(WebApplication app, string? role)
    {
        var client = app.GetTestClient();
        if (role is not null)
        {
            var token = app.Services.GetRequiredService<JwtTokenGenerator>().Create(new User { UserId = 1, Email = "test@example.com", Role = new Role { RoleName = role } });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }
        return client;
    }

    [Theory]
    [InlineData(null, 401, 401)]
    [InlineData(RoleNames.Admin, 200, 200)]
    [InlineData(RoleNames.FinancialManager, 200, 200)]
    [InlineData(RoleNames.CollectionOfficer, 200, 200)]
    [InlineData(RoleNames.Auditor, 200, 403)]
    [InlineData(RoleNames.SalesAgent, 200, 403)]
    [InlineData("Unknown", 403, 403)]
    public async Task Roles(string? role, int read, int queue)
    {
        await using var app = await Host(); using var client = Client(app, role);
        foreach (var path in new[] { "/api/installments", "/api/installments/1", "/api/contracts/1/installments" })
            Assert.Equal(read, (int)(await client.GetAsync(path)).StatusCode);
        Assert.Equal(queue, (int)(await client.GetAsync("/api/collections/due")).StatusCode);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=2147483647")]
    [InlineData("contractId=0")]
    [InlineData("customerId=-1")]
    [InlineData("contractId=abc")]
    [InlineData("status=2")]
    [InlineData("status=partial")]
    [InlineData("dueFrom=2028-02-30")]
    [InlineData("dueTo=06-15-2028")]
    [InlineData("dueFrom=2028-06-16&dueTo=2028-06-15")]
    [InlineData("installmentNumber=13")]
    [InlineData("pastDueOnly=yes")]
    [InlineData("openOnly=true&status=Paid")]
    [InlineData("openOnly=true&status=Waived")]
    [InlineData("pastDueOnly=true&status=Paid")]
    [InlineData("search=%00")]
    public async Task BadQueries(string query)
    {
        await using var app = await Host(); using var client = Client(app, RoleNames.Admin);
        foreach (var path in new[] { "/api/installments?", "/api/collections/due?" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + query)).StatusCode);
    }

    [Theory]
    [InlineData("contractId=1", 6)]
    [InlineData("customerId=2", 1)]
    [InlineData("status=Overdue", 1)]
    [InlineData("status=PartiallyPaid", 1)]
    [InlineData("status=Partially%20Paid", 1)]
    [InlineData("dueFrom=2028-06-15&dueTo=2028-06-15", 1)]
    [InlineData("contractId=1&pastDueOnly=true", 3)]
    [InlineData("contractId=1&openOnly=true", 4)]
    [InlineData("contractId=1&installmentNumber=4", 1)]
    [InlineData("search=cnt-one", 6)]
    [InlineData("search=EXAMPLE", 6)]
    [InlineData("search=%25", 0)]
    [InlineData("contractId=1&customerId=2", 0)]
    public async Task Filters(string query, int expected)
    {
        await using var app = await Host(); using var client = Client(app, RoleNames.Admin);
        var page = (await client.GetFromJsonAsync<InstallmentPage>("/api/installments?" + query))!;
        Assert.Equal(expected, page.TotalCount); Assert.Equal(expected, page.Items.Count);
    }

    [Fact]
    public async Task DetailsQueuePaginationAndReadOnly()
    {
        await using var app = await Host(); using var client = Client(app, RoleNames.Admin);
        var first = (await client.GetFromJsonAsync<InstallmentPage>("/api/installments?pageSize=2"))!;
        var second = (await client.GetFromJsonAsync<InstallmentPage>("/api/installments?pageSize=2&page=2"))!;
        Assert.Equal(7, first.TotalCount); Assert.Equal(2, first.Items.Count); Assert.Equal(2, second.Items.Count);
        Assert.Empty(first.Items.Select(x => x.InstallmentId).Intersect(second.Items.Select(x => x.InstallmentId)));
        var queue = (await client.GetFromJsonAsync<InstallmentPage>("/api/collections/due"))!;
        Assert.Equal(new long[] { 1, 2, 3 }, queue.Items.Select(x => x.InstallmentId));
        Assert.True(queue.Items[0].IsPastDue); Assert.False(queue.Items[2].IsPastDue);
        Assert.Equal(new DateOnly(2028, 6, 15), queue.AsOfUtcDate);
        var partial = (await client.GetFromJsonAsync<InstallmentDetails>("/api/installments/2"))!.Installment;
        Assert.Equal("Partially Paid", partial.Status); Assert.Equal(4m, partial.PaidAmount); Assert.Equal(6m, partial.RemainingAmount);
        var paid = (await client.GetFromJsonAsync<InstallmentDetails>("/api/installments/5"))!.Installment;
        Assert.False(paid.IsOpen); Assert.False(paid.IsPastDue);
        Assert.Equal(6, (await client.GetFromJsonAsync<ContractSchedule>("/api/contracts/1/installments"))!.Items.Count);
        foreach (var path in new[] { "/api/installments/999", "/api/contracts/999/installments" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        foreach (var path in new[] { "/api/installments/0", "/api/contracts/-1/installments" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PutAsJsonAsync("/api/installments/1", new { status = "Paid" })).StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInstallmentService>();
        await service.ListAsync(new(), false, default); await service.GetAsync(1, default); await service.ScheduleAsync(1, default);
        var db = scope.ServiceProvider.GetRequiredService<ImsDbContext>();
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(InstallmentStatus.Pending, (await db.Installments.AsNoTracking().SingleAsync(x => x.InstallmentId == 1)).Status);
    }
}

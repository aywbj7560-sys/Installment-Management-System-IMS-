using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IMS.API.Extensions;
using IMS.Application.Authentication;
using IMS.Application.Payments;
using IMS.Domain.Entities;
using IMS.Infrastructure.Authentication;
using IMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IMS.Tests;

public class PaymentTests
{
    private static async Task<WebApplication> Host()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "payment-test-only-signing-key-at-least-32-bytes", ["Jwt:Issuer"] = "tests", ["Jwt:Audience"] = "tests"
        });
        builder.Services.AddImsAuthentication(builder.Configuration);
        builder.Services.AddDbContext<ImsDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        builder.Services.AddScoped<IPaymentService, StubPaymentService>();
        var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapPaymentEndpoints();
        await app.StartAsync();
        return app;
    }

    private static HttpClient Client(WebApplication app, string? role)
    {
        var client = app.GetTestClient();
        if (role is not null)
        {
            var token = app.Services.GetRequiredService<JwtTokenGenerator>().Create(new User
                { UserId = 1, Email = "payments@example.com", Role = new Role { RoleName = role } });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }
        return client;
    }

    [Theory]
    [InlineData(null, 401, 401)]
    [InlineData(RoleNames.Admin, 200, 201)]
    [InlineData(RoleNames.FinancialManager, 200, 201)]
    [InlineData(RoleNames.CollectionOfficer, 200, 201)]
    [InlineData(RoleNames.Auditor, 200, 403)]
    [InlineData(RoleNames.SalesAgent, 403, 403)]
    [InlineData("Unknown", 403, 403)]
    public async Task Roles(string? role, int read, int write)
    {
        await using var app = await Host(); using var client = Client(app, role);
        foreach (var path in new[] { "/api/payments", "/api/payments/1", "/api/contracts/1/payments" })
            Assert.Equal(read, (int)(await client.GetAsync(path)).StatusCode);
        Assert.Equal(write, (int)(await client.PostAsJsonAsync("/api/payments", Valid())).StatusCode);
    }

    public static PaymentRequest Valid() => new() { ContractId = 1, PaymentReference = "receipt", Amount = 1, PaymentMethod = "Cash" };

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"contractId\":1,\"paymentReference\":\"x\",\"amount\":1,\"paymentMethod\":\"Cash\",\"allocations\":[{\"installmentId\":999,\"allocatedAmount\":2}]}")]
    [InlineData("{\"contractId\":1,\"paymentReference\":\"x\",\"amount\":1,\"paymentMethod\":\"Cash\",\"paymentDate\":\"invalid\"}")]
    public async Task InvalidBody(string body)
    {
        await using var app = await Host(); using var client = Client(app, RoleNames.Admin);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/payments",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"))).StatusCode);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=2147483647")]
    [InlineData("contractId=0")]
    [InlineData("customerId=-1")]
    public async Task InvalidQuery(string query)
    {
        await using var app = await Host(); using var client = Client(app, RoleNames.Admin);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/payments?" + query)).StatusCode);
    }

    [Fact]
    public void Validation()
    {
        Assert.Empty(Valid().Validate());
        foreach (var (name, value) in new (string, object?)[] {
            ("Amount", 0m), ("Amount", -1m), ("Amount", 0.001m), ("Amount", 10000000000000m),
            ("ContractId", 0L), ("PaymentReference", " "), ("PaymentReference", new string('x',51)),
            ("PaymentMethod", ""), ("PaymentMethod", new string('x',31)), ("Notes", "bad\0text"),
            ("PaymentDate", DateTimeOffset.MinValue), ("PaymentDate", DateTimeOffset.MaxValue) })
        {
            var request = Valid(); typeof(PaymentRequest).GetProperty(name)!.SetValue(request, value);
            Assert.NotEmpty(request.Validate());
        }
    }
}

public sealed class StubPaymentService : IPaymentService
{
    private static PaymentDetails Details() => new(new(1, "receipt", 1, 1, DateTime.UtcNow, 1, "Cash", null), new(1, "contract", 1, "customer", 10, "Active"), []);
    public Task<PaymentResult> CreateAsync(PaymentRequest request, long userId, CancellationToken ct) => Task.FromResult(new PaymentResult(Details()));
    public Task<PaymentDetails?> GetAsync(long id, CancellationToken ct) => Task.FromResult<PaymentDetails?>(Details());
    public Task<PaymentPage> ListAsync(string? search, long? contractId, long? customerId, int page, int pageSize, CancellationToken ct) => Task.FromResult(new PaymentPage([], 0, page, pageSize));
    public Task<bool> ContractExistsAsync(long id, CancellationToken ct) => Task.FromResult(true);
}

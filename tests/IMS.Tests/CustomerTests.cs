using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IMS.API.Extensions;
using IMS.Application.Authentication;
using IMS.Application.Customers;
using IMS.Domain.Entities;
using IMS.Infrastructure.Authentication;
using IMS.Infrastructure.Customers;
using IMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace IMS.Tests;

public class CustomerTests
{
    private static CustomerRequest Request(string identity = "ID-123", string status = "Active", string phone = "07701234567") => new()
    { FullName = "Test Customer", IdentificationNumber = identity, Phone = phone, Address = "Baghdad", Status = status };

    private static async Task<WebApplication> Host()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "customer-test-only-signing-key-at-least-32-bytes", ["Jwt:Issuer"] = "tests", ["Jwt:Audience"] = "tests"
        });
        builder.Services.AddImsAuthentication(builder.Configuration);
        var database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<ImsDbContext>(o => o.UseInMemoryDatabase(database));
        builder.Services.AddScoped<ICustomerService, CustomerService>();
        var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapCustomerEndpoints();
        await app.StartAsync();
        return app;
    }

    private static HttpClient Client(WebApplication app, string? role)
    {
        var client = app.GetTestClient();
        if (role is not null)
        {
            var token = app.Services.GetRequiredService<JwtTokenGenerator>().Create(new User
            { UserId = 1, Email = "customer-tests@example.com", Role = new Role { RoleName = role } });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }
        return client;
    }

    [Theory]
    [InlineData(null, 401, 401)]
    [InlineData(RoleNames.Admin, 200, 201)]
    [InlineData(RoleNames.FinancialManager, 200, 201)]
    [InlineData(RoleNames.SalesAgent, 200, 201)]
    [InlineData(RoleNames.CollectionOfficer, 200, 403)]
    [InlineData(RoleNames.Auditor, 200, 403)]
    [InlineData("Unknown", 403, 403)]
    public async Task EveryEndpointEnforcesRoles(string? role, int read, int write)
    {
        await using var app = await Host();
        using var admin = Client(app, RoleNames.Admin);
        var created = await admin.PostAsJsonAsync("/api/customers", Request());
        var customer = (await created.Content.ReadFromJsonAsync<CustomerResponse>())!;
        using var client = Client(app, role);
        Assert.Equal(read, (int)(await client.GetAsync("/api/customers")).StatusCode);
        Assert.Equal(read, (int)(await client.GetAsync($"/api/customers/{customer.CustomerId}")).StatusCode);
        Assert.Equal(write, (int)(await client.PostAsJsonAsync("/api/customers", Request("another"))).StatusCode);
        Assert.Equal(write == 201 ? 200 : write, (int)(await client.PutAsJsonAsync($"/api/customers/{customer.CustomerId}", Request(status: "Inactive"))).StatusCode);
    }

    [Fact]
    public async Task CreateReadUpdateSearchAndConflicts()
    {
        await using var app = await Host();
        using var client = Client(app, RoleNames.Admin);
        var created = await client.PostAsJsonAsync("/api/customers", Request());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var customer = (await created.Content.ReadFromJsonAsync<CustomerResponse>())!;
        Assert.Equal($"/api/customers/{customer.CustomerId}", created.Headers.Location!.ToString());
        Assert.Equal("Active", customer.Status);
        Assert.DoesNotContain("contracts", await created.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/customers", Request())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/customers/9999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/customers/9999", Request())).StatusCode);
        foreach (var status in new[] { "Inactive", "Blacklisted", "Active" })
        {
            var update = await client.PutAsJsonAsync(created.Headers.Location, Request(status: status, phone: "07800000000"));
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            var saved = (await client.GetFromJsonAsync<CustomerResponse>(created.Headers.Location))!;
            Assert.Equal(status, saved.Status); Assert.Equal("07800000000", saved.Phone);
            Assert.Equal(customer.CreatedAt, saved.CreatedAt);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(created.Headers.Location, Request("changed"))).StatusCode);
        foreach (var search in new[] { "test CUSTOMER", "ID-123", "078000", customer.CustomerId.ToString() })
            Assert.Single((await client.GetFromJsonAsync<CustomerPage>("/api/customers?search=" + Uri.EscapeDataString(search)))!.Items);
        foreach (var query in new[] { "search=missing", "status=Blacklisted", "page=2&pageSize=1", "search=%25", "search=%5F" })
            Assert.Empty((await client.GetFromJsonAsync<CustomerPage>("/api/customers?" + query))!.Items);
        var page = (await client.GetFromJsonAsync<CustomerPage>("/api/customers?pageSize=1&status=Active"))!;
        Assert.Equal(1, page.TotalCount); Assert.Single(page.Items);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"fullName\":\"  \"}")]
    public async Task InvalidBodiesReturn400(string json)
    {
        await using var app = await Host(); using var client = Client(app, RoleNames.Admin);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/customers", new StringContent(json, System.Text.Encoding.UTF8, "application/json"))).StatusCode);
    }

    [Theory]
    [InlineData("status=0")]
    [InlineData("status=Unknown")]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=2147483647&pageSize=100")]
    [InlineData("page=invalid")]
    public async Task InvalidQueriesReturn400(string query)
    {
        await using var app = await Host(); using var client = Client(app, RoleNames.Admin);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/customers?" + query)).StatusCode);
    }

    [Fact]
    public void ValidationMatchesSchemaAndRejectsInvalidStatusEmailAndNullCharacters()
    {
        Assert.Empty(Request().Validate());
        foreach (var property in new[] { "FullName", "IdentificationNumber", "Phone", "SecondaryPhone", "Email" })
        {
            var request = Request();
            typeof(CustomerRequest).GetProperty(property)!.SetValue(request, new string('x', 151));
            Assert.Contains(property, request.Validate().Keys);
        }
        foreach (var property in new[] { "FullName", "IdentificationNumber", "Phone", "Address", "Status" })
        {
            var request = Request(); typeof(CustomerRequest).GetProperty(property)!.SetValue(request, "  ");
            Assert.Contains(property, request.Validate().Keys);
        }
        foreach (var status in new[] { "0", "active", "Unknown" }) Assert.Contains("Status", Request(status: status).Validate().Keys);
        var email = Request(); typeof(CustomerRequest).GetProperty("Email")!.SetValue(email, "invalid");
        Assert.Contains("Email", email.Validate().Keys);
        Assert.Contains("Phone", Request(phone: "abc\0").Validate().Keys);
    }
}

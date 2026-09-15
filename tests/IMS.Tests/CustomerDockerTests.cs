using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IMS.Application.Authentication;
using IMS.Application.Customers;
using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IMS.Tests;

public sealed class LiveCustomerFactAttribute : FactAttribute
{
    public LiveCustomerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("IMS_LIVE_TESTS") != "1")
            Skip = "Set IMS_LIVE_TESTS=1 and provide the Compose API environment to run live verification.";
    }
}

public class CustomerDockerTests
{
    [LiveCustomerFact]
    public async Task LiveApiAndPostgresRegression()
    {
        using var client = new HttpClient { BaseAddress = new Uri("http://api:8080") };
        Assert.Equal("IMS API is running", await client.GetStringAsync("/"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/database")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/customers")).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = Environment.GetEnvironmentVariable("SeedAdmin__Email")!,
            Password = Environment.GetEnvironmentVariable("SeedAdmin__Password")!
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        await using var db = new ImsDbContext(new DbContextOptionsBuilder<ImsDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")).Options);
        var identity = "IMS-TEST-" + Guid.NewGuid().ToString("N");
        CustomerRequest Request(string status = "Active") => new()
        { FullName = "IMS Integration Test", IdentificationNumber = identity, Phone = "07700000000", SecondaryPhone = "07811111111", Email = "ims-test@example.com", Address = "Test address", Status = status };
        try
        {
            var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/customers", Request()), client.PostAsJsonAsync("/api/customers", Request()));
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            var created = responses.Single(r => r.StatusCode == HttpStatusCode.Created);
            var customer = (await created.Content.ReadFromJsonAsync<CustomerResponse>())!;
            Assert.True(customer.CreatedAt > DateTime.UtcNow.AddMinutes(-2));
            Assert.Equal(DateTimeKind.Utc, customer.CreatedAt.Kind);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(created.Headers.Location)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/customers", new { })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/customers/9223372036854775807")).StatusCode);
            foreach (var status in new[] { "Inactive", "Blacklisted", "Active" })
            {
                Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(created.Headers.Location, Request(status))).StatusCode);
                var page = (await client.GetFromJsonAsync<CustomerPage>($"/api/customers?search={identity}&status={status}"))!;
                Assert.Single(page.Items); Assert.Equal(status, page.Items[0].Status);
            }
            foreach (var term in new[] { "integration TEST", "07811111111", "IMS-TEST@EXAMPLE.COM" })
            {
                var page = (await client.GetFromJsonAsync<CustomerPage>("/api/customers?search=" + Uri.EscapeDataString(term)))!;
                Assert.Contains(page.Items, c => c.CustomerId == customer.CustomerId);
            }
            // Exercise the provider's unique-constraint translation independently of the precheck.
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Customers.Add(new IMS.Domain.Entities.Customer
            { FullName = "duplicate", IdentificationNumber = identity, Phone = "0", Address = "test" });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            var postgres = Assert.IsType<Npgsql.PostgresException>(exception.InnerException);
            Assert.Equal("23505", postgres.SqlState);
            Assert.Equal("customers_identification_number_key", postgres.ConstraintName);
            await transaction.RollbackAsync();
            db.ChangeTracker.Clear();
        }
        finally
        {
            await db.Customers.Where(c => c.IdentificationNumber == identity).ExecuteDeleteAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/database")).StatusCode);
    }
}

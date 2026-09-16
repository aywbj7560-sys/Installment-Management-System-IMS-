using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IMS.Application.Authentication;
using IMS.Application.Users;
using IMS.Domain.Entities;
using IMS.Infrastructure.Authentication;
using IMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IMS.Tests;

[Collection("Reports PostgreSQL")]
public class UserAdministrationDockerTests
{
    [LiveCustomerFact]
    public async Task UsersPersistAuthorizeDeactivateAndPreserveHistory()
    {
        using var client = new HttpClient { BaseAddress = new Uri("http://api:8080") };
        var adminLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        { Email = Environment.GetEnvironmentVariable("SeedAdmin__Email")!, Password = Environment.GetEnvironmentVariable("SeedAdmin__Password")! });
        Assert.Equal(HttpStatusCode.OK, adminLogin.StatusCode);
        var adminAuthentication = (await adminLogin.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            adminAuthentication.Token);
        await using var db = new ImsDbContext(new DbContextOptionsBuilder<ImsDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")).Options);
        var tag = "UTEST-" + Guid.NewGuid().ToString("N");
        const string password = "user-docker-test-password";
        long customerId = 0, contractId = 0, paymentId = 0;
        CreateUserRequest Request(string suffix, string role = RoleNames.SalesAgent, string? email = null) => new()
        { Username = tag + suffix, Email = email ?? $"{tag}{suffix}@example.com", FullName = "Docker User " + suffix,
            Password = password, Role = role, IsActive = true };
        try
        {
            var createdResponse = await client.PostAsJsonAsync("/api/users", Request("main"));
            Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
            var created = (await createdResponse.Content.ReadFromJsonAsync<UserResponse>())!;
            var json = await createdResponse.Content.ReadAsStringAsync();
            Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
            var stored = await db.Users.AsNoTracking().SingleAsync(x => x.UserId == created.UserId);
            Assert.NotEqual(password, stored.PasswordHash);
            Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<User>().VerifyHashedPassword(stored, stored.PasswordHash, password));

            var duplicateEmail = $"{tag}race@example.com";
            var concurrent = await Task.WhenAll(client.PostAsJsonAsync("/api/users", Request("race1", email: duplicateEmail)),
                client.PostAsJsonAsync("/api/users", Request("race2", email: duplicateEmail)));
            Assert.Single(concurrent, x => x.StatusCode == HttpStatusCode.Created);
            Assert.Single(concurrent, x => x.StatusCode == HttpStatusCode.Conflict);

            var jwt = new JwtTokenGenerator(new JwtSettings
            {
                Key = Environment.GetEnvironmentVariable("Jwt__Key")!,
                Issuer = Environment.GetEnvironmentVariable("Jwt__Issuer")!,
                Audience = Environment.GetEnvironmentVariable("Jwt__Audience")!,
                ExpirationMinutes = int.Parse(Environment.GetEnvironmentVariable("Jwt__ExpirationMinutes")!)
            });
            foreach (var role in new[] { RoleNames.FinancialManager, RoleNames.SalesAgent, RoleNames.CollectionOfficer, RoleNames.Auditor })
            {
                using var denied = new HttpClient { BaseAddress = client.BaseAddress };
                denied.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                    jwt.Create(new User { UserId = created.UserId, Email = created.Email, Role = new Role { RoleName = role } }).Token);
                Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync("/api/users")).StatusCode);
            }

            var roles = (await client.GetFromJsonAsync<RoleResponse[]>("/api/roles"))!;
            Assert.Equal(RoleNames.All, roles.Select(x => x.RoleName));
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsJsonAsync("/api/roles", new { roleName = "Other" })).StatusCode);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync($"/api/users/{created.UserId}")).StatusCode);

            var customer = new Customer { FullName = tag, IdentificationNumber = tag, Phone = "0", Address = "test" };
            var contract = new Contract { ContractNumber = tag, Customer = customer, CreatedByUserId = created.UserId,
                ContractDate = DateTime.UtcNow, TotalAmount = 10, RemainingAmount = 9 };
            var payment = new Payment { PaymentReference = tag, Contract = contract, ReceivedByUserId = created.UserId,
                PaymentDate = DateTime.UtcNow, Amount = 1, PaymentMethod = "Cash" };
            db.Add(payment); await db.SaveChangesAsync(); customerId = customer.CustomerId; contractId = contract.ContractId; paymentId = payment.PaymentId;
            var update = new UpdateUserRequest { Username = created.Username, Email = created.Email, FullName = created.FullName,
                Role = created.Role, IsActive = false };
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/users/{created.UserId}", update)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await new HttpClient { BaseAddress = client.BaseAddress }.PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Email = created.Email, Password = password })).StatusCode);
            db.ChangeTracker.Clear();
            Assert.Equal(created.UserId, (await db.Contracts.AsNoTracking().SingleAsync(x => x.ContractId == contractId)).CreatedByUserId);
            Assert.Equal(created.UserId, (await db.Payments.AsNoTracking().SingleAsync(x => x.PaymentId == paymentId)).ReceivedByUserId);
            update = new UpdateUserRequest { Username = created.Username, Email = created.Email, FullName = created.FullName,
                Role = created.Role, IsActive = true };
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/users/{created.UserId}", update)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await new HttpClient { BaseAddress = client.BaseAddress }.PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Email = created.Email, Password = password })).StatusCode);
            var adminId = adminAuthentication.User.Id;
            var admin = (await client.GetFromJsonAsync<UserResponse>($"/api/users/{adminId}"))!;
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/users/{adminId}", new UpdateUserRequest
            { Username = admin.Username, Email = admin.Email, FullName = admin.FullName, Role = admin.Role, IsActive = false })).StatusCode);
        }
        finally
        {
            if (paymentId > 0) await db.Payments.Where(x => x.PaymentId == paymentId).ExecuteDeleteAsync();
            if (contractId > 0) await db.Contracts.Where(x => x.ContractId == contractId).ExecuteDeleteAsync();
            if (customerId > 0) await db.Customers.Where(x => x.CustomerId == customerId).ExecuteDeleteAsync();
            await db.Users.Where(x => x.Username.StartsWith(tag)).ExecuteDeleteAsync();
        }
    }
}

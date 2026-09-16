using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IMS.Application.Authentication;
using IMS.Application.Contracts;
using IMS.Application.Guarantors;
using IMS.Domain.Entities;
using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IMS.Tests;

[CollectionDefinition("Guarantors PostgreSQL", DisableParallelization = true)]
public sealed class GuarantorPostgresCollection { }

[Collection("Guarantors PostgreSQL")]
public class GuarantorDockerTests
{
    [LiveCustomerFact]
    public async Task PersistenceSearchAndHistoricalLinkSafety()
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
        var key = "GTEST-" + Guid.NewGuid().ToString("N");
        var customer = new Customer { FullName = key, IdentificationNumber = key, Phone = "0", Address = "test" };
        var product = new Product { ProductCode = key, Name = key, CashPrice = 120 };
        db.AddRange(customer, product); await db.SaveChangesAsync();
        GuarantorRequest Request(string suffix = "", bool? active = null, bool edited = false) => new()
        {
            FullName = key, IdentificationNumber = key + suffix, Phone = edited ? "07800000000" : "07700000000",
            SecondaryPhone = "07912345678", Address = edited ? "New address" : "Baghdad", Occupation = edited ? "Engineer" : "Teacher",
            Workplace = edited ? "Office" : "School", Notes = edited ? "New profile note" : "Profile note", IsActive = active
        };
        ContractRequest ContractRequest(long guarantorId, string suffix = "") => new()
        {
            ContractNumber = key + suffix, CustomerId = customer.CustomerId, ContractDate = DateTimeOffset.UtcNow,
            Items = [new(product.ProductId, 1, 120)], GuarantorId = guarantorId, GuaranteeNotes = "Historical guarantee note"
        };
        try
        {
            var races = await Task.WhenAll(client.PostAsJsonAsync("/api/guarantors", Request()), client.PostAsJsonAsync("/api/guarantors", Request()));
            var created = Assert.Single(races, x => x.StatusCode == HttpStatusCode.Created);
            Assert.Single(races, x => x.StatusCode == HttpStatusCode.Conflict);
            var guarantor = (await created.Content.ReadFromJsonAsync<GuarantorResponse>())!;
            Assert.True(guarantor.IsActive); Assert.True(guarantor.CreatedAt > DateTime.UtcNow.AddMinutes(-2));
            Assert.True(await db.Guarantors.AnyAsync(x => x.GuarantorId == guarantor.GuarantorId));
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/guarantors", Request("second", false))).StatusCode);
            foreach (var term in new[] { key.ToLowerInvariant(), "077000", "079123", guarantor.GuarantorId.ToString() })
                Assert.Contains((await client.GetFromJsonAsync<GuarantorPage>("/api/guarantors?search=" + term))!.Items, x => x.GuarantorId == guarantor.GuarantorId);
            var page = (await client.GetFromJsonAsync<GuarantorPage>($"/api/guarantors?search={key}&pageSize=1"))!;
            var second = (await client.GetFromJsonAsync<GuarantorPage>($"/api/guarantors?search={key}&pageSize=1&page=2"))!;
            Assert.Equal(2, page.TotalCount); Assert.Single(page.Items); Assert.Single(second.Items);
            Assert.NotEqual(page.Items[0].GuarantorId, second.Items[0].GuarantorId);
            Assert.Single((await client.GetFromJsonAsync<GuarantorPage>($"/api/guarantors?search={key}&isActive=false"))!.Items);
            Assert.Single((await client.GetFromJsonAsync<GuarantorPage>($"/api/guarantors?search={key}&isActive=true"))!.Items);
            Assert.Empty((await client.GetFromJsonAsync<GuarantorPage>($"/api/guarantors?search={key}%25"))!.Items);

            var contractResponse = await client.PostAsJsonAsync("/api/contracts", ContractRequest(guarantor.GuarantorId));
            Assert.Equal(HttpStatusCode.Created, contractResponse.StatusCode);
            var before = (await contractResponse.Content.ReadFromJsonAsync<ContractDetails>())!;
            var linksBefore = (await client.GetFromJsonAsync<GuarantorDetails>(created.Headers.Location))!.Contracts;
            Assert.Equal(before.Contract.ContractId, Assert.Single(linksBefore).ContractId);
            foreach (var active in new[] { false, true })
            {
                var update = await client.PutAsJsonAsync(created.Headers.Location, Request(active: active, edited: true));
                Assert.Equal(HttpStatusCode.OK, update.StatusCode);
                var saved = (await client.GetFromJsonAsync<GuarantorDetails>(created.Headers.Location))!;
                Assert.Equal(guarantor.CreatedAt, saved.Guarantor.CreatedAt); Assert.Equal(active, saved.Guarantor.IsActive);
                Assert.Equal("07800000000", saved.Guarantor.Phone); Assert.Equal("New address", saved.Guarantor.Address);
                Assert.Equal("Engineer", saved.Guarantor.Occupation); Assert.Equal("Office", saved.Guarantor.Workplace);
                Assert.Equal("New profile note", saved.Guarantor.Notes); Assert.Equal(linksBefore, saved.Contracts);
                var after = (await client.GetFromJsonAsync<ContractDetails>(contractResponse.Headers.Location))!;
                Assert.Equal(before.Contract, after.Contract); Assert.Equal(before.Items, after.Items); Assert.Equal(before.Installments, after.Installments);
                Assert.Equal("Historical guarantee note", Assert.Single(after.Guarantors).GuaranteeNotes);
                if (!active)
                    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/contracts", ContractRequest(guarantor.GuarantorId, "inactive"))).StatusCode);
            }
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/contracts", ContractRequest(guarantor.GuarantorId, "again"))).StatusCode);
            Assert.Equal(2, (await client.GetFromJsonAsync<GuarantorDetails>(created.Headers.Location))!.Contracts.Count);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(created.Headers.Location, Request("second"))).StatusCode);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync(created.Headers.Location)).StatusCode);
            Assert.Equal(2, await db.ContractGuarantors.CountAsync(x => x.GuarantorId == guarantor.GuarantorId));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/guarantors/9223372036854775807")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/guarantors/9223372036854775807", Request())).StatusCode);
        }
        finally
        {
            await db.Contracts.Where(x => x.CustomerId == customer.CustomerId).ExecuteDeleteAsync();
            await db.Guarantors.Where(x => x.IdentificationNumber.StartsWith(key)).ExecuteDeleteAsync();
            await db.Products.Where(x => x.ProductId == product.ProductId).ExecuteDeleteAsync();
            await db.Customers.Where(x => x.CustomerId == customer.CustomerId).ExecuteDeleteAsync();
        }
    }
}

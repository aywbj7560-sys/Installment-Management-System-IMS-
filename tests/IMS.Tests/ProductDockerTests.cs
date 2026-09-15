using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IMS.Application.Authentication;
using IMS.Application.Products;
using IMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IMS.Tests;

public class ProductDockerTests
{
    [LiveCustomerFact]
    public async Task LiveProductPersistenceSearchDefaultsAndConflicts()
    {
        using var client = new HttpClient { BaseAddress = new Uri("http://api:8080") };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/products")).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        { Email = Environment.GetEnvironmentVariable("SeedAdmin__Email")!, Password = Environment.GetEnvironmentVariable("SeedAdmin__Password")! });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        await using var db = new ImsDbContext(new DbContextOptionsBuilder<ImsDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")).Options);
        var code = "IMS-PTEST-" + Guid.NewGuid().ToString("N");
        var otherCode = code + "-2";
        ProductRequest Request(string productCode, bool active = false) => new()
        { ProductCode = productCode, Name = code + " appliance", Description = code + " cooling", CashPrice = 9999999999999.99m, InstallmentPrice = 0, IsActive = active };
        try
        {
            var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/products", Request(code)), client.PostAsJsonAsync("/api/products", Request(code)));
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            var created = responses.Single(r => r.StatusCode == HttpStatusCode.Created);
            var product = (await created.Content.ReadFromJsonAsync<ProductResponse>())!;
            Assert.False(product.IsActive); Assert.Equal(9999999999999.99m, product.CashPrice); Assert.Equal(0m, product.InstallmentPrice);
            Assert.True(product.CreatedAt > DateTime.UtcNow.AddMinutes(-2)); Assert.Equal(DateTimeKind.Utc, product.CreatedAt.Kind);
            var persisted = (await client.GetFromJsonAsync<ProductResponse>(created.Headers.Location))!;
            Assert.False(persisted.IsActive); Assert.Equal(product, persisted);
            var second = await client.PostAsJsonAsync("/api/products", new { productCode = otherCode, name = code + " default", cashPrice = 0 });
            Assert.Equal(HttpStatusCode.Created, second.StatusCode);
            var defaults = (await second.Content.ReadFromJsonAsync<ProductResponse>())!;
            Assert.True(defaults.IsActive); Assert.Null(defaults.InstallmentPrice);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(second.Headers.Location, Request(code))).StatusCode);
            foreach (var search in new[] { code.ToLowerInvariant(), code + " APPLIANCE", code + " COOLING" })
                Assert.Contains((await client.GetFromJsonAsync<ProductPage>("/api/products?search=" + Uri.EscapeDataString(search)))!.Items, p => p.ProductId == product.ProductId);
            Assert.Single((await client.GetFromJsonAsync<ProductPage>($"/api/products?search={code}&isActive=false"))!.Items);
            var firstPage = (await client.GetFromJsonAsync<ProductPage>($"/api/products?search={code}&pageSize=1"))!;
            var secondPage = (await client.GetFromJsonAsync<ProductPage>($"/api/products?search={code}&pageSize=1&page=2"))!;
            Assert.Equal(2, firstPage.TotalCount); Assert.Single(firstPage.Items); Assert.Single(secondPage.Items);
            Assert.NotEqual(firstPage.Items[0].ProductId, secondPage.Items[0].ProductId);
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(created.Headers.Location, Request(code, true))).StatusCode);
            Assert.True((await client.GetFromJsonAsync<ProductResponse>(created.Headers.Location))!.IsActive);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/products", new { productCode = "invalid", name = "invalid", cashPrice = -1 })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/products?isActive=Unknown")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/products/9223372036854775807")).StatusCode);
        }
        finally
        {
            await db.Products.Where(p => p.ProductCode == code || p.ProductCode == otherCode).ExecuteDeleteAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/database")).StatusCode);
    }
}

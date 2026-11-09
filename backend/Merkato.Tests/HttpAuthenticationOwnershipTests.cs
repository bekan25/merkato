using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Merkato.Domain.Entities;
using Merkato.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Merkato.Tests;

/// <summary>
/// HTTP-host integration tests. They exercise routing, JWT validation, authorization
/// middleware and database-backed ownership filters through real HTTP requests.
/// The CI backend job supplies an isolated PostgreSQL service connection.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MerkatoHttpCollection : ICollectionFixture<MerkatoHttpHostFixture>
{
    public const string Name = "MERKATO HTTP host";
}

public sealed class MerkatoHttpHostFixture : WebApplicationFactory<Program>
{
    public MerkatoHttpHostFixture()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSql");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "HTTP-host tests require ConnectionStrings__PostgreSql. The MERKATO CI backend job provides PostgreSQL.");

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("Jwt__Key", "ci-only-key-with-more-than-thirty-two-random-characters");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "https://api.merkato.test");
        Environment.SetEnvironmentVariable("Jwt__Audience", "https://merkato.test");
        Environment.SetEnvironmentVariable("MERKATO_ALLOWED_ORIGINS", "https://merkato.test");
        Environment.SetEnvironmentVariable("MERKATO_APPLY_MIGRATIONS", "true");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }
}

[Collection(MerkatoHttpCollection.Name)]
public sealed class HttpAuthenticationOwnershipTests(MerkatoHttpHostFixture factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task JwtRolesAndOrderOwnershipAreEnforcedOverHttp()
    {
        using var client = factory.CreateClient();

        // Authentication middleware must reject anonymous access and role middleware
        // must reject authenticated users with the wrong role.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/orders/mine")).StatusCode);

        var buyerA = await RegisterAsync(client, "BUYER");
        var buyerB = await RegisterAsync(client, "BUYER");
        var merchantA = await RegisterAsync(client, "MERCHANT");
        var merchantB = await RegisterAsync(client, "MERCHANT");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", merchantA.Token);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/orders/mine")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerA.Token);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/orders/merchant")).StatusCode);

        // Seed two independent buyer/merchant pairs using the same production EF model.
        long orderAId;
        long orderBId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MerkatoDbContext>();
            var orderA = new Order
            {
                OrderNumber = "HTTP-" + Guid.NewGuid().ToString("N")[..12],
                BuyerId = buyerA.UserId,
                SellerId = merchantA.UserId,
                ProductTitle = "HTTP ownership item A",
                Quantity = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var orderB = new Order
            {
                OrderNumber = "HTTP-" + Guid.NewGuid().ToString("N")[..12],
                BuyerId = buyerB.UserId,
                SellerId = merchantB.UserId,
                ProductTitle = "HTTP ownership item B",
                Quantity = 1,
                CreatedAt = DateTime.UtcNow.AddSeconds(1),
                UpdatedAt = DateTime.UtcNow
            };
            db.Orders.AddRange(orderA, orderB);
            await db.SaveChangesAsync();
            orderAId = orderA.Id;
            orderBId = orderB.Id;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerA.Token);
        var buyerOrders = await ReadOrderIdsAsync(client, "/api/orders/mine");
        Assert.Equal(new[] { orderAId }, buyerOrders);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", merchantA.Token);
        var merchantOrders = await ReadOrderIdsAsync(client, "/api/orders/merchant");
        Assert.Equal(new[] { orderAId }, merchantOrders);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", buyerB.Token);
        Assert.Equal(new[] { orderBId }, await ReadOrderIdsAsync(client, "/api/orders/mine"));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", merchantB.Token);
        Assert.Equal(new[] { orderBId }, await ReadOrderIdsAsync(client, "/api/orders/merchant"));
    }

    private static async Task<(long UserId, string Token)> RegisterAsync(HttpClient client, string role)
    {
        var phone = "09" + Random.Shared.Next(10_000_000, 99_999_999).ToString();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            phoneNumber = phone,
            fullName = "HTTP Test " + role,
            role,
            pin = "123456"
        });
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        return (
            root.GetProperty("userId").GetInt64(),
            root.GetProperty("token").GetString() ?? throw new InvalidOperationException("Registration returned no JWT.")
        );
    }

    private static async Task<long[]> ReadOrderIdsAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt64())
            .ToArray();
    }
}

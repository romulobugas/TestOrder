using System.Net.Http.Json;
using Dapper;
using MySqlConnector;

namespace TestOrder.Api.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class SeedIntegrationTests(MySqlContainerFixture fixture)
{
    [Fact]
    public async Task Seed_CreatesProducts()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        var productCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM products");
        Assert.Equal(50, productCount);
    }

    [Fact]
    public async Task Seed_CreatesAtLeast3000Orders()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        var orderCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM orders");
        Assert.True(orderCount >= 3000, $"Expected at least 3000 orders, got {orderCount}.");
    }

    [Fact]
    public async Task MySqlContainer_IsReachable()
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        var result = await connection.ExecuteScalarAsync<int>("SELECT 1");
        Assert.Equal(1, result);
    }
}

using System.Net;
using System.Net.Http.Json;
using Dapper;
using MySqlConnector;

namespace TestOrder.Api.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class RevenueEndpointTests(MySqlContainerFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    // Intervalo do seed (DatabaseSeeder): ReferenceDate 2026-07-01 UTC menos até 364 dias,
    // ou seja, pedidos distribuídos entre 2025-07-02 e 2026-07-01 (inclusive), 365 dias.
    private static readonly DateOnly SeedRangeStart = new(2025, 7, 2);
    private static readonly DateOnly SeedRangeEnd = new(2026, 7, 1);

    [Fact]
    public async Task GetDailyRevenue_ValidRange_ReturnsAggregatedDays()
    {
        await using var connection = await OpenConnectionAsync();
        var productId = await CreateProductAsync(connection, "Revenue ValidRange", 20m);

        var day1 = new DateOnly(2030, 3, 10);
        var day2 = day1.AddDays(1);
        var day3 = day1.AddDays(2);

        // Pedido exatamente no início do primeiro dia do intervalo (00:00:00 UTC).
        await InsertOrderWithItemAsync(connection, day1.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), productId, quantity: 2, unitPrice: 20m);
        // Pedido perto do fim do último dia do intervalo — ainda deve contar em day3 (limite semiaberto end+1dia).
        await InsertOrderWithItemAsync(connection, day3.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc), productId, quantity: 3, unitPrice: 20m);

        var response = await GetDailyRevenueAsync(day1, day3);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DailyRevenueDto>();
        Assert.NotNull(result);
        Assert.Equal(3, result.Days.Count);

        Assert.Equal(day1.ToString("yyyy-MM-dd"), result.Days[0].Date);
        Assert.Equal(40m, result.Days[0].Revenue);
        Assert.Equal(1, result.Days[0].OrderCount);

        Assert.Equal(day2.ToString("yyyy-MM-dd"), result.Days[1].Date);
        Assert.Equal(0m, result.Days[1].Revenue);
        Assert.Equal(0, result.Days[1].OrderCount);

        Assert.Equal(day3.ToString("yyyy-MM-dd"), result.Days[2].Date);
        Assert.Equal(60m, result.Days[2].Revenue);
        Assert.Equal(1, result.Days[2].OrderCount);
    }

    [Fact]
    public async Task GetDailyRevenue_SingleDayRange_ReturnsExactlyOneDay()
    {
        await using var connection = await OpenConnectionAsync();
        var productId = await CreateProductAsync(connection, "Revenue SingleDay", 15m);

        var dayWithOrder = new DateOnly(2030, 4, 1);
        await InsertOrderWithItemAsync(connection, dayWithOrder.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), productId, quantity: 4, unitPrice: 15m);

        var responseWithOrder = await GetDailyRevenueAsync(dayWithOrder, dayWithOrder);
        Assert.Equal(HttpStatusCode.OK, responseWithOrder.StatusCode);

        var resultWithOrder = await responseWithOrder.Content.ReadFromJsonAsync<DailyRevenueDto>();
        Assert.NotNull(resultWithOrder);
        Assert.Single(resultWithOrder.Days);
        Assert.Equal(60m, resultWithOrder.Days[0].Revenue);
        Assert.Equal(1, resultWithOrder.Days[0].OrderCount);
        Assert.Equal(60m, resultWithOrder.TotalRevenue);
        Assert.Equal(1, resultWithOrder.TotalOrders);

        var dayWithoutOrder = new DateOnly(2031, 5, 15);
        var responseEmpty = await GetDailyRevenueAsync(dayWithoutOrder, dayWithoutOrder);
        Assert.Equal(HttpStatusCode.OK, responseEmpty.StatusCode);

        var resultEmpty = await responseEmpty.Content.ReadFromJsonAsync<DailyRevenueDto>();
        Assert.NotNull(resultEmpty);
        Assert.Single(resultEmpty.Days);
        Assert.Equal(0m, resultEmpty.Days[0].Revenue);
        Assert.Equal(0, resultEmpty.Days[0].OrderCount);
        Assert.Equal(0m, resultEmpty.TotalRevenue);
        Assert.Equal(0, resultEmpty.TotalOrders);
    }

    [Fact]
    public async Task GetDailyRevenue_TotalRevenue_MatchesSumOfDays()
    {
        await using var connection = await OpenConnectionAsync();
        var productId = await CreateProductAsync(connection, "Revenue TotalSum", 8m);

        var baseDate = new DateOnly(2030, 6, 1);
        await InsertOrderWithItemAsync(connection, baseDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), productId, quantity: 1, unitPrice: 8m);
        await InsertOrderWithItemAsync(connection, baseDate.AddDays(2).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), productId, quantity: 2, unitPrice: 8m);
        await InsertOrderWithItemAsync(connection, baseDate.AddDays(2).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), productId, quantity: 3, unitPrice: 8m);

        var response = await GetDailyRevenueAsync(baseDate, baseDate.AddDays(4));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DailyRevenueDto>();
        Assert.NotNull(result);
        Assert.Equal(5, result.Days.Count);

        Assert.Equal(result.Days.Sum(d => d.Revenue), result.TotalRevenue);
        Assert.Equal(result.Days.Sum(d => d.OrderCount), result.TotalOrders);
        Assert.Equal(48m, result.TotalRevenue);
        Assert.Equal(3, result.TotalOrders);
    }

    [Fact]
    public async Task GetDailyRevenue_EmptyRange_ReturnsZeroedDays()
    {
        var start = new DateOnly(2099, 1, 1);
        var end = new DateOnly(2099, 1, 7);

        var response = await GetDailyRevenueAsync(start, end);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DailyRevenueDto>();
        Assert.NotNull(result);
        Assert.Equal(0m, result.TotalRevenue);
        Assert.Equal(0, result.TotalOrders);
        Assert.Equal(7, result.Days.Count);
        Assert.All(result.Days, day =>
        {
            Assert.Equal(0m, day.Revenue);
            Assert.Equal(0, day.OrderCount);
        });
    }

    [Fact]
    public async Task GetDailyRevenue_MissingStartDate_Returns400()
    {
        var response = await _client.GetAsync("/api/revenue/daily?endDate=2030-01-10");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error.Error));
    }

    [Fact]
    public async Task GetDailyRevenue_MissingEndDate_Returns400()
    {
        var response = await _client.GetAsync("/api/revenue/daily?startDate=2030-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error.Error));
    }

    [Theory]
    [InlineData("2030/01/01", "2030-01-10")]
    [InlineData("2030-01-01", "01-10-2030")]
    [InlineData("2030-13-40", "2030-01-10")]
    [InlineData("2030-02-30", "2030-01-10")]
    [InlineData("", "2030-01-10")]
    public async Task GetDailyRevenue_InvalidDate_Returns400(string startDate, string endDate)
    {
        var response = await _client.GetAsync($"/api/revenue/daily?startDate={startDate}&endDate={endDate}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error.Error));
    }

    [Fact]
    public async Task GetDailyRevenue_StartAfterEnd_Returns400()
    {
        var response = await GetDailyRevenueAsync(new DateOnly(2030, 2, 1), new DateOnly(2030, 1, 1));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error.Error));
    }

    [Fact]
    public async Task GetDailyRevenue_RangeBoundary_AcceptsUpTo366AndRejectsOver()
    {
        var start = new DateOnly(2030, 1, 1);

        var exactly366 = await GetDailyRevenueAsync(start, start.AddDays(365));
        Assert.Equal(HttpStatusCode.OK, exactly366.StatusCode);

        var result366 = await exactly366.Content.ReadFromJsonAsync<DailyRevenueDto>();
        Assert.NotNull(result366);
        Assert.Equal(366, result366.Days.Count);

        var over366 = await GetDailyRevenueAsync(start, start.AddDays(366));
        Assert.Equal(HttpStatusCode.BadRequest, over366.StatusCode);

        var error = await over366.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error.Error));
    }

    [Fact]
    public async Task Regression_Modules001And002_StillWork()
    {
        var productsResponse = await _client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.OK, productsResponse.StatusCode);
        var products = await productsResponse.Content.ReadFromJsonAsync<List<ProductDto>>();
        Assert.NotNull(products);
        Assert.NotEmpty(products);

        var ordersResponse = await _client.GetAsync("/api/orders?page=1&pageSize=5");
        Assert.Equal(HttpStatusCode.OK, ordersResponse.StatusCode);
        var page = await ordersResponse.Content.ReadFromJsonAsync<PagedOrdersDto>();
        Assert.NotNull(page);
        Assert.NotEmpty(page.Items);

        var orderId = page.Items[0].Id;
        var orderResponse = await _client.GetAsync($"/api/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, orderResponse.StatusCode);

        var productId = products[0].Id;
        var createResponse = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequestDto(null, [
            new CreateOrderItemRequestDto(productId, 1)
        ]));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var revenueTodayResponse = await GetDailyRevenueAsync(today, today);
        Assert.Equal(HttpStatusCode.OK, revenueTodayResponse.StatusCode);
        var revenueToday = await revenueTodayResponse.Content.ReadFromJsonAsync<DailyRevenueDto>();
        Assert.NotNull(revenueToday);
        Assert.Single(revenueToday.Days);
        Assert.True(revenueToday.Days[0].OrderCount >= 1);

        // Confirma que o endpoint também agrega os pedidos do seed do módulo 001 (não apenas dados de teste isolados).
        var seedResponse = await GetDailyRevenueAsync(SeedRangeStart, SeedRangeEnd);
        Assert.Equal(HttpStatusCode.OK, seedResponse.StatusCode);
        var seedResult = await seedResponse.Content.ReadFromJsonAsync<DailyRevenueDto>();
        Assert.NotNull(seedResult);
        Assert.True(seedResult.TotalOrders > 0);
        Assert.True(seedResult.TotalRevenue > 0);
    }

    private async Task<HttpResponseMessage> GetDailyRevenueAsync(DateOnly startDate, DateOnly endDate) =>
        await _client.GetAsync($"/api/revenue/daily?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

    private async Task<MySqlConnection> OpenConnectionAsync()
    {
        var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<int> CreateProductAsync(MySqlConnection connection, string name, decimal unitPrice) =>
        await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO products (name, unit_price, stock_quantity)
            VALUES (@Name, @UnitPrice, 0);
            SELECT LAST_INSERT_ID();
            """,
            new { Name = name, UnitPrice = unitPrice });

    private static async Task<long> InsertOrderWithItemAsync(
        MySqlConnection connection,
        DateTime createdAtUtc,
        int productId,
        int quantity,
        decimal unitPrice,
        string status = "created")
    {
        var orderId = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO orders (created_at, status) VALUES (@CreatedAt, @Status);
            SELECT LAST_INSERT_ID();
            """,
            new { CreatedAt = createdAtUtc, Status = status });

        await connection.ExecuteAsync(
            "INSERT INTO order_items (order_id, product_id, quantity, unit_price) VALUES (@OrderId, @ProductId, @Quantity, @UnitPrice)",
            new { OrderId = orderId, ProductId = productId, Quantity = quantity, UnitPrice = unitPrice });

        return orderId;
    }
}

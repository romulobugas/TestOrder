using System.Net;
using System.Net.Http.Json;
using Dapper;
using MySqlConnector;

namespace TestOrder.Api.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class OrdersEndpointTests(MySqlContainerFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GetOrders_Returns200WithPaginationMetadata()
    {
        var response = await _client.GetAsync("/api/orders?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedOrdersDto>();
        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.True(page.TotalCount >= 3000);
        Assert.True(page.Items.Count <= 20);
        Assert.True(page.TotalPages >= (int)Math.Ceiling(page.TotalCount / 20.0));
    }

    [Fact]
    public async Task GetOrders_WithoutQueryUsesDefaults()
    {
        var response = await _client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedOrdersDto>();
        Assert.NotNull(page);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.True(page.Items.Count <= 20);
    }

    [Fact]
    public async Task GetOrders_IsOrderedByCreatedAtDescending()
    {
        var response = await _client.GetAsync("/api/orders?page=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedOrdersDto>();
        Assert.NotNull(page);
        Assert.True(page.Items.Count > 1);

        for (var i = 0; i < page.Items.Count - 1; i++)
        {
            var current = page.Items[i];
            var next = page.Items[i + 1];

            Assert.True(
                current.CreatedAt > next.CreatedAt
                || (current.CreatedAt == next.CreatedAt && current.Id > next.Id),
                $"Order at index {i} should sort before index {i + 1}.");
        }
    }

    [Fact]
    public async Task GetOrders_PageBeyondEnd_ReturnsEmptyItems()
    {
        var response = await _client.GetAsync("/api/orders?page=999&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedOrdersDto>();
        Assert.NotNull(page);
        Assert.Equal(999, page.Page);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task OrderTotal_EqualsSumOfLineItems()
    {
        var response = await _client.GetAsync("/api/orders?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedOrdersDto>();
        Assert.NotNull(page);

        foreach (var order in page.Items)
        {
            var expectedTotal = order.Items.Sum(item => item.Quantity * item.UnitPrice);
            Assert.Equal(expectedTotal, order.Total);
        }
    }

    [Fact]
    public async Task Page1AndPage2_DoNotShareOrderIds()
    {
        var page1Response = await _client.GetAsync("/api/orders?page=1&pageSize=20");
        var page2Response = await _client.GetAsync("/api/orders?page=2&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, page1Response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, page2Response.StatusCode);

        var page1 = await page1Response.Content.ReadFromJsonAsync<PagedOrdersDto>();
        var page2 = await page2Response.Content.ReadFromJsonAsync<PagedOrdersDto>();

        Assert.NotNull(page1);
        Assert.NotNull(page2);

        var overlap = page1.Items.Select(o => o.Id).Intersect(page2.Items.Select(o => o.Id)).ToList();
        Assert.Empty(overlap);
    }

    [Theory]
    [InlineData("/api/orders?page=0&pageSize=20")]
    [InlineData("/api/orders?page=1&pageSize=0")]
    [InlineData("/api/orders?page=1&pageSize=101")]
    public async Task InvalidPagination_Returns400WithError(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error.Error));
    }

    [Fact]
    public async Task GetOrders_CreatedAt_SerializesAsUtcWithZ()
    {
        var response = await _client.GetAsync("/api/orders?page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var createdAt = document.RootElement.GetProperty("items")[0].GetProperty("createdAt").GetString();

        Assert.NotNull(createdAt);
        Assert.EndsWith("Z", createdAt);
        Assert.DoesNotContain("+", createdAt);
        Assert.DoesNotContain("-", createdAt[10..]); // no offset after date portion

        var page = await response.Content.ReadFromJsonAsync<PagedOrdersDto>();
        Assert.NotNull(page);
        Assert.Equal(DateTimeKind.Utc, page.Items[0].CreatedAt.Kind);
    }

    [Fact]
    public async Task OrderById_CreatedAt_SerializesAsUtcWithZ()
    {
        var listResponse = await _client.GetAsync("/api/orders?page=1&pageSize=1");
        var listPage = await listResponse.Content.ReadFromJsonAsync<PagedOrdersDto>();
        Assert.NotNull(listPage);
        Assert.NotEmpty(listPage.Items);

        var orderId = listPage.Items[0].Id;
        var response = await _client.GetAsync($"/api/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var createdAt = document.RootElement.GetProperty("createdAt").GetString();

        Assert.NotNull(createdAt);
        Assert.EndsWith("Z", createdAt);
    }

    [Fact]
    public async Task OrderById_ExistingOrder_Returns200WithItems()
    {
        var listResponse = await _client.GetAsync("/api/orders?page=1&pageSize=1");
        var listPage = await listResponse.Content.ReadFromJsonAsync<PagedOrdersDto>();
        Assert.NotNull(listPage);
        Assert.NotEmpty(listPage.Items);

        var orderId = listPage.Items[0].Id;
        var response = await _client.GetAsync($"/api/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal(orderId, order.Id);
        Assert.NotEmpty(order.Items);
        Assert.False(string.IsNullOrWhiteSpace(order.Status));
    }

    [Fact]
    public async Task OrderById_MissingOrder_Returns404()
    {
        var response = await _client.GetAsync("/api/orders/99999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOrders_FilterByStatus_ReturnsOnlyMatchingStatus()
    {
        var response = await _client.GetAsync("/api/orders?page=1&pageSize=20&status=processed");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedOrdersDto>();
        Assert.NotNull(page);
        Assert.True(page.TotalCount > 0, "Seed deve conter pedidos com status 'processed' (~10%).");
        Assert.All(page.Items, order => Assert.Equal("processed", order.Status));
    }

    [Fact]
    public async Task GetOrders_EmptyStatus_BehavesAsNoFilter()
    {
        var filtered = await _client.GetAsync("/api/orders?page=1&pageSize=20&status=");
        var unfiltered = await _client.GetAsync("/api/orders?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unfiltered.StatusCode);

        var filteredPage = await filtered.Content.ReadFromJsonAsync<PagedOrdersDto>();
        var unfilteredPage = await unfiltered.Content.ReadFromJsonAsync<PagedOrdersDto>();

        Assert.NotNull(filteredPage);
        Assert.NotNull(unfilteredPage);
        Assert.Equal(unfilteredPage.TotalCount, filteredPage.TotalCount);
    }

    [Fact]
    public async Task GetOrders_FilterByDateRange_ReturnsOnlyOrdersWithinRange()
    {
        await using var connection = await OpenConnectionAsync();
        var productId = await CreateProductAsync(connection, "Orders Filter DateRange", 5m);

        var day1 = new DateOnly(2033, 1, 1);
        var day2 = day1.AddDays(1);
        var outsideDay = day1.AddDays(10);

        var insideOrderId = await InsertOrderWithItemAsync(connection, day1.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc), productId, 1, 5m);
        var insideOrderId2 = await InsertOrderWithItemAsync(connection, day2.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc), productId, 1, 5m);
        var outsideOrderId = await InsertOrderWithItemAsync(connection, outsideDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), productId, 1, 5m);

        var response = await _client.GetAsync(
            $"/api/orders?page=1&pageSize=50&startDate={day1:yyyy-MM-dd}&endDate={day2:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedOrdersDto>();
        Assert.NotNull(page);

        var ids = page.Items.Select(o => o.Id).ToHashSet();
        Assert.Contains(insideOrderId, ids);
        Assert.Contains(insideOrderId2, ids);
        Assert.DoesNotContain(outsideOrderId, ids);
    }

    [Fact]
    public async Task GetOrders_FilterStartAfterEnd_Returns400()
    {
        var response = await _client.GetAsync("/api/orders?startDate=2030-02-01&endDate=2030-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error.Error));
    }

    [Theory]
    [InlineData("startDate=2030/01/01")]
    [InlineData("endDate=01-10-2030")]
    [InlineData("startDate=2030-13-40")]
    public async Task GetOrders_FilterInvalidDateFormat_Returns400(string queryParam)
    {
        var response = await _client.GetAsync($"/api/orders?{queryParam}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error.Error));
    }

    [Fact]
    public async Task GetOrders_FilterWithoutDates_DoesNotLimitByDate()
    {
        // status vazio + datas vazias = mesmo resultado que sem filtro nenhum (todos os pedidos paginados).
        var response = await _client.GetAsync("/api/orders?page=1&pageSize=20&status=&startDate=&endDate=");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedOrdersDto>();
        Assert.NotNull(page);
        Assert.True(page.TotalCount >= 3000);
    }

    [Fact]
    public async Task GetOrders_FilterKeepsPaginationMetadataConsistent()
    {
        var page1 = await _client.GetAsync("/api/orders?page=1&pageSize=10&status=created");
        var page2 = await _client.GetAsync("/api/orders?page=2&pageSize=10&status=created");

        Assert.Equal(HttpStatusCode.OK, page1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, page2.StatusCode);

        var page1Dto = await page1.Content.ReadFromJsonAsync<PagedOrdersDto>();
        var page2Dto = await page2.Content.ReadFromJsonAsync<PagedOrdersDto>();

        Assert.NotNull(page1Dto);
        Assert.NotNull(page2Dto);
        Assert.Equal(page1Dto.TotalCount, page2Dto.TotalCount);

        var overlap = page1Dto.Items.Select(o => o.Id).Intersect(page2Dto.Items.Select(o => o.Id)).ToList();
        Assert.Empty(overlap);
        Assert.All(page1Dto.Items.Concat(page2Dto.Items), order => Assert.Equal("created", order.Status));
    }

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

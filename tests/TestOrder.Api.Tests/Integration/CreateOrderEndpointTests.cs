using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using MySqlConnector;

namespace TestOrder.Api.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class CreateOrderEndpointTests(MySqlContainerFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task CreateOrder_Success_Returns201AndPersistsOrder()
    {
        var productId = await CreateControlledProductAsync("CreateOrder Success", 25.50m, availableUnits: 10);

        var response = await PostOrderJsonAsync(new CreateOrderRequestDto("Demo Customer", [
            new CreateOrderItemRequestDto(productId, 2)
        ]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains($"/api/orders/", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);

        var created = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal("created", created.Status);
        Assert.Equal(51.00m, created.Total);
        Assert.Single(created.Items);
        Assert.Equal(productId, created.Items[0].ProductId);
        Assert.Equal(2, created.Items[0].Quantity);
        Assert.Equal(25.50m, created.Items[0].UnitPrice);

        var getResponse = await _client.GetAsync($"/api/orders/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(created.Total, fetched.Total);
        Assert.Equal(created.Items.Count, fetched.Items.Count);

        await using var connection = await OpenConnectionAsync();
        var orderItemCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM order_items WHERE order_id = @OrderId",
            new { OrderId = created.Id });
        Assert.Equal(1, orderItemCount);

        var reservationCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM order_reservation_units WHERE order_id = @OrderId",
            new { OrderId = created.Id });
        Assert.Equal(2, reservationCount);

        var reservedUnitsCount = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM inventory_units iu
            INNER JOIN order_reservation_units oru ON oru.inventory_unit_id = iu.id
            WHERE oru.order_id = @OrderId AND iu.status = 'reserved'
            """,
            new { OrderId = created.Id });
        Assert.Equal(2, reservedUnitsCount);

        var withoutNameResponse = await PostOrderJsonAsync(new CreateOrderRequestDto(null, [
            new CreateOrderItemRequestDto(productId, 1)
        ]));
        Assert.Equal(HttpStatusCode.Created, withoutNameResponse.StatusCode);
        var withoutNameOrder = await withoutNameResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(withoutNameOrder);

        var emptyNameResponse = await PostOrderJsonAsync(new CreateOrderRequestDto("   ", [
            new CreateOrderItemRequestDto(productId, 1)
        ]));
        Assert.Equal(HttpStatusCode.Created, emptyNameResponse.StatusCode);
        var emptyNameOrder = await emptyNameResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(emptyNameOrder);

        var customerName = await connection.ExecuteScalarAsync<string?>(
            "SELECT customer_name FROM orders WHERE id = @OrderId",
            new { OrderId = emptyNameOrder.Id });
        Assert.Null(customerName);
    }

    [Fact]
    public async Task CreateOrder_InvalidPayload_EmptyItems_Returns400()
    {
        var before = await CaptureSnapshotAsync();

        var response = await PostOrderRawAsync("""{"items":[]}""");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error.Error));

        var after = await CaptureSnapshotAsync();
        AssertSnapshotUnchanged(before, after);
    }

    [Theory]
    [InlineData("QuantityZero", 0)]
    [InlineData("QuantityNegative", -1)]
    public async Task CreateOrder_InvalidPayload_InvalidQuantity_Returns400(string scenario, int quantity)
    {
        var productId = await CreateControlledProductAsync($"InvalidPayload {scenario}", 10m, availableUnits: 3);
        var before = await CaptureSnapshotAsync();

        var response = await PostOrderJsonAsync(new CreateOrderRequestDto(null, [
            new CreateOrderItemRequestDto(productId, quantity)
        ]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error.Error));

        var after = await CaptureSnapshotAsync();
        AssertSnapshotUnchanged(before, after);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CreateOrder_InvalidPayload_InvalidProductId_Returns400(int productId)
    {
        var before = await CaptureSnapshotAsync();

        var response = await PostOrderJsonAsync(new CreateOrderRequestDto(null, [
            new CreateOrderItemRequestDto(productId, 1)
        ]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error.Error));

        var after = await CaptureSnapshotAsync();
        AssertSnapshotUnchanged(before, after);
    }

    [Fact]
    public async Task CreateOrder_InvalidPayload_EmptyBody_Returns400()
    {
        var before = await CaptureSnapshotAsync();

        var response = await PostOrderRawAsync("");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error.Error));

        var after = await CaptureSnapshotAsync();
        AssertSnapshotUnchanged(before, after);
    }

    [Fact]
    public async Task CreateOrder_InvalidPayload_MissingProduct_Returns400()
    {
        var before = await CaptureSnapshotAsync();
        var missingProductId = 88_888_888;

        var response = await PostOrderJsonAsync(new CreateOrderRequestDto(null, [
            new CreateOrderItemRequestDto(missingProductId, 1)
        ]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error.Error));

        var after = await CaptureSnapshotAsync();
        AssertSnapshotUnchanged(before, after);
    }

    [Theory]
    [InlineData("""{"items":""")]
    [InlineData("""not-json""")]
    public async Task CreateOrder_InvalidPayload_MalformedJson_Returns400(string json)
    {
        var before = await CaptureSnapshotAsync();

        var response = await PostOrderRawAsync(json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var after = await CaptureSnapshotAsync();
        AssertSnapshotUnchanged(before, after);
    }

    [Fact]
    public async Task CreateOrder_DuplicateProduct_Returns400()
    {
        var productId = await CreateControlledProductAsync("Duplicate Product", 12m, availableUnits: 5);
        var before = await CaptureSnapshotAsync();

        var response = await PostOrderJsonAsync(new CreateOrderRequestDto(null, [
            new CreateOrderItemRequestDto(productId, 1),
            new CreateOrderItemRequestDto(productId, 2)
        ]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.NotNull(error);
        Assert.Contains("duplicate", error.Error, StringComparison.OrdinalIgnoreCase);

        var after = await CaptureSnapshotAsync();
        AssertSnapshotUnchanged(before, after);
    }

    [Fact]
    public async Task CreateOrder_InsufficientStock_Returns409()
    {
        var productId = await CreateControlledProductAsync("Insufficient Stock", 15m, availableUnits: 2);
        var before = await CaptureSnapshotAsync();

        var response = await PostOrderJsonAsync(new CreateOrderRequestDto(null, [
            new CreateOrderItemRequestDto(productId, 5)
        ]));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorDto>();
        Assert.NotNull(error);
        Assert.False(string.IsNullOrWhiteSpace(error.Error));

        await using var connection = await OpenConnectionAsync();

        var availableUnits = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM inventory_units WHERE product_id = @ProductId AND status = 'available'",
            new { ProductId = productId });
        Assert.Equal(2, availableUnits);

        var reservationsForProduct = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM order_reservation_units oru
            INNER JOIN inventory_units iu ON iu.id = oru.inventory_unit_id
            WHERE iu.product_id = @ProductId
            """,
            new { ProductId = productId });
        Assert.Equal(0, reservationsForProduct);

        var orderItemsForProduct = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM order_items WHERE product_id = @ProductId",
            new { ProductId = productId });
        Assert.Equal(0, orderItemsForProduct);

        var after = await CaptureSnapshotAsync();
        Assert.Equal(before.OrderCount, after.OrderCount);
        Assert.Equal(before.AvailableUnits, after.AvailableUnits);
        Assert.Equal(before.ReservedUnits, after.ReservedUnits);
        Assert.Equal(before.ReservationLinks, after.ReservationLinks);
        Assert.Equal(before.ProcessingEvents, after.ProcessingEvents);
    }

    [Fact]
    public async Task CreateOrder_WritesPendingOutboxEvent()
    {
        var productId = await CreateControlledProductAsync("Outbox Event", 9.99m, availableUnits: 3);

        var response = await PostOrderJsonAsync(new CreateOrderRequestDto(null, [
            new CreateOrderItemRequestDto(productId, 1)
        ]));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(created);

        await using var connection = await OpenConnectionAsync();
        var events = (await connection.QueryAsync<OutboxEventRow>(
            """
            SELECT event_type AS EventType, status AS Status, payload AS Payload
            FROM order_processing_events
            WHERE order_id = @OrderId
            """,
            new { OrderId = created.Id })).ToList();

        Assert.Single(events);
        Assert.Equal("OrderCreated", events[0].EventType);
        Assert.Equal("pending", events[0].Status);
        Assert.Contains($"\"orderId\":{created.Id}", events[0].Payload.Replace(" ", "", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateOrder_ConcurrentRequests_DoNotOverbook()
    {
        const int stockUnits = 5;
        const int parallelRequests = 10;
        var productId = await CreateControlledProductAsync("Concurrent Product", 7.50m, availableUnits: stockUnits);

        var tasks = Enumerable.Range(0, parallelRequests)
            .Select(_ => PostOrderJsonAsync(new CreateOrderRequestDto(null, [
                new CreateOrderItemRequestDto(productId, 1)
            ])))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        var createdCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(stockUnits, createdCount);
        Assert.Equal(parallelRequests - stockUnits, conflictCount);

        await using var connection = await OpenConnectionAsync();

        var reservedUnits = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM inventory_units WHERE product_id = @ProductId AND status = 'reserved'",
            new { ProductId = productId });
        Assert.Equal(stockUnits, reservedUnits);

        var reservationLinks = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM order_reservation_units oru
            INNER JOIN inventory_units iu ON iu.id = oru.inventory_unit_id
            WHERE iu.product_id = @ProductId
            """,
            new { ProductId = productId });
        Assert.Equal(stockUnits, reservationLinks);

        var duplicateAssignments = await connection.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM (
                SELECT oru.inventory_unit_id
                FROM order_reservation_units oru
                INNER JOIN inventory_units iu ON iu.id = oru.inventory_unit_id
                WHERE iu.product_id = @ProductId
                GROUP BY oru.inventory_unit_id
                HAVING COUNT(*) > 1
            ) duplicates
            """,
            new { ProductId = productId });
        Assert.Equal(0, duplicateAssignments);
    }

    [Fact]
    public async Task Regression_Module001_ReadEndpointsStillWork()
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
        Assert.True(page.TotalCount >= 3000);
        Assert.True(page.Items.Count <= 5);

        var orderId = page.Items[0].Id;
        var orderResponse = await _client.GetAsync($"/api/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, orderResponse.StatusCode);

        var order = await orderResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal(orderId, order.Id);
        Assert.NotEmpty(order.Items);
    }

    private async Task<HttpResponseMessage> PostOrderJsonAsync(CreateOrderRequestDto request) =>
        await _client.PostAsJsonAsync("/api/orders", request);

    private async Task<HttpResponseMessage> PostOrderRawAsync(string json) =>
        await _client.PostAsync(
            "/api/orders",
            new StringContent(json, Encoding.UTF8, "application/json"));

    private async Task<MySqlConnection> OpenConnectionAsync()
    {
        var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private async Task<int> CreateControlledProductAsync(string name, decimal unitPrice, int availableUnits)
    {
        await using var connection = await OpenConnectionAsync();

        var productId = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO products (name, unit_price, stock_quantity)
            VALUES (@Name, @UnitPrice, 0);
            SELECT LAST_INSERT_ID();
            """,
            new { Name = name, UnitPrice = unitPrice });

        await InsertAvailableUnitsAsync(connection, productId, availableUnits);
        return productId;
    }

    private static async Task InsertAvailableUnitsAsync(MySqlConnection connection, int productId, int count)
    {
        const int batchSize = 100;

        for (var inserted = 0; inserted < count; inserted += batchSize)
        {
            var currentBatch = Math.Min(batchSize, count - inserted);
            var values = string.Join(
                ", ",
                Enumerable.Range(0, currentBatch).Select(_ => $"({productId}, 'available')"));

            await connection.ExecuteAsync(
                $"INSERT INTO inventory_units (product_id, status) VALUES {values}");
        }
    }

    private async Task<DbSnapshot> CaptureSnapshotAsync()
    {
        await using var connection = await OpenConnectionAsync();
        return new DbSnapshot(
            await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM orders"),
            await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM inventory_units WHERE status = 'available'"),
            await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM inventory_units WHERE status = 'reserved'"),
            await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM order_reservation_units"),
            await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM order_processing_events"));
    }

    private static void AssertSnapshotUnchanged(DbSnapshot before, DbSnapshot after)
    {
        Assert.Equal(before.OrderCount, after.OrderCount);
        Assert.Equal(before.AvailableUnits, after.AvailableUnits);
        Assert.Equal(before.ReservedUnits, after.ReservedUnits);
        Assert.Equal(before.ReservationLinks, after.ReservationLinks);
        Assert.Equal(before.ProcessingEvents, after.ProcessingEvents);
    }

    private sealed record DbSnapshot(
        long OrderCount,
        long AvailableUnits,
        long ReservedUnits,
        long ReservationLinks,
        long ProcessingEvents);

    private sealed record OutboxEventRow(string EventType, string Status, string Payload);
}

using System.Net;
using System.Net.Http.Json;

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
}

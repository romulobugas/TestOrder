using System.Net;
using System.Net.Http.Json;

namespace TestOrder.Api.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public class ProductsEndpointTests(MySqlContainerFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GetProducts_Returns200WithExpectedFields()
    {
        var response = await _client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();
        Assert.NotNull(products);
        Assert.NotEmpty(products);

        foreach (var product in products)
        {
            Assert.True(product.Id > 0);
            Assert.False(string.IsNullOrWhiteSpace(product.Name));
            Assert.True(product.UnitPrice > 0);
        }
    }
}

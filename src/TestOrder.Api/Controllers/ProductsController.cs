using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using TestOrder.Api.Models.Responses;

namespace TestOrder.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(MySqlConnection connection) : ControllerBase
{
    private const string ListProductsSql = """
        SELECT id AS Id, name AS Name, unit_price AS UnitPrice
        FROM products
        ORDER BY id
        """;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetProducts(CancellationToken cancellationToken)
    {
        await connection.OpenAsync(cancellationToken);
        var products = await connection.QueryAsync<ProductResponse>(ListProductsSql);
        return Ok(products.ToList());
    }
}

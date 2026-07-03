using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using TestOrder.Api.Models.Responses;

namespace TestOrder.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController(MySqlConnection connection) : ControllerBase
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [HttpGet]
    public async Task<ActionResult<PagedOrdersResponse>> GetOrders(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var paginationError = ValidatePagination(page, pageSize, out var resolvedPage, out var resolvedPageSize);
        if (paginationError is not null)
        {
            return BadRequest(paginationError);
        }

        await connection.OpenAsync(cancellationToken);

        var totalCount = await connection.ExecuteScalarAsync<int>(OrdersQueries.CountOrders);
        var offset = (resolvedPage - 1) * resolvedPageSize;

        var orderRows = (await connection.QueryAsync<OrderPageRow>(
            OrdersQueries.PageOrders,
            new { PageSize = resolvedPageSize, Offset = offset })).ToList();

        var itemsByOrderId = await LoadItemsForOrdersAsync(orderRows.Select(o => o.Id));

        var orders = orderRows
            .Select(row => ToOrderResponse(row, itemsByOrderId.GetValueOrDefault(row.Id)))
            .ToList();

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)resolvedPageSize);

        return Ok(new PagedOrdersResponse(
            resolvedPage,
            resolvedPageSize,
            totalCount,
            totalPages,
            orders));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<OrderResponse>> GetOrderById(long id, CancellationToken cancellationToken)
    {
        await connection.OpenAsync(cancellationToken);

        var order = await connection.QuerySingleOrDefaultAsync<OrderDetailRow>(
            OrdersQueries.OrderById,
            new { Id = id });

        if (order is null)
        {
            return NotFound(new ErrorResponse("Order not found."));
        }

        var items = (await connection.QueryAsync<OrderItemDetailRow>(
            OrdersQueries.OrderItemsByOrderId,
            new { OrderId = id })).ToList();

        return Ok(ToOrderResponse(order, items));
    }

    private ErrorResponse? ValidatePagination(int? page, int? pageSize, out int resolvedPage, out int resolvedPageSize)
    {
        resolvedPage = page ?? DefaultPage;
        resolvedPageSize = pageSize ?? DefaultPageSize;

        if (resolvedPage < 1)
        {
            return new ErrorResponse("page must be greater than or equal to 1.");
        }

        if (resolvedPageSize < 1 || resolvedPageSize > MaxPageSize)
        {
            return new ErrorResponse("pageSize must be between 1 and 100.");
        }

        return null;
    }

    private async Task<Dictionary<long, List<OrderItemResponse>>> LoadItemsForOrdersAsync(
        IEnumerable<long> orderIds)
    {
        var ids = orderIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await connection.QueryAsync<OrderItemRow>(
            OrdersQueries.OrderItemsByOrderIds,
            new { OrderIds = ids });

        return rows
            .GroupBy(row => row.OrderId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => new OrderItemResponse(
                    item.ProductId,
                    item.ProductName,
                    item.Quantity,
                    item.UnitPrice)).ToList());
    }

    private static OrderResponse ToOrderResponse(OrderPageRow row, List<OrderItemResponse>? items) =>
        new(row.Id, row.CreatedAt, row.Status, row.Total, items ?? []);

    private static OrderResponse ToOrderResponse(OrderDetailRow row, List<OrderItemDetailRow> items) =>
        new(
            row.Id,
            row.CreatedAt,
            row.Status,
            row.Total,
            items.Select(item => new OrderItemResponse(
                item.ProductId,
                item.ProductName,
                item.Quantity,
                item.UnitPrice)).ToList());
}

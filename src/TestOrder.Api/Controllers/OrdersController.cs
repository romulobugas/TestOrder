using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using TestOrder.Api.Models.Requests;
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

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> CreateOrder(
        [FromBody] CreateOrderRequest? request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateCreateOrderRequest(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var normalizedRequest = new CreateOrderRequest(
            NormalizeCustomerName(request!.CustomerName),
            request.Items);

        await connection.OpenAsync(cancellationToken);

        var result = await CreateOrderCommands.ExecuteAsync(connection, normalizedRequest, cancellationToken);

        return result switch
        {
            CreateOrderSuccess success => CreatedAtAction(
                nameof(GetOrderById),
                new { id = success.OrderId },
                new OrderResponse(
                    success.OrderId,
                    success.CreatedAt,
                    success.Status,
                    success.Total,
                    success.Items)),
            CreateOrderBadRequest badRequest => BadRequest(new ErrorResponse(badRequest.Message)),
            CreateOrderConflict conflict => Conflict(new ErrorResponse(conflict.Message)),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse("Unexpected error creating order."))
        };
    }

    private static ErrorResponse? ValidateCreateOrderRequest(CreateOrderRequest? request)
    {
        if (request is null)
        {
            return new ErrorResponse("request body is required.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return new ErrorResponse("items must contain at least one entry.");
        }

        var seenProductIds = new HashSet<int>();

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
            {
                return new ErrorResponse("quantity must be greater than 0.");
            }

            if (item.ProductId <= 0)
            {
                return new ErrorResponse("productId must be greater than 0.");
            }

            if (!seenProductIds.Add(item.ProductId))
            {
                return new ErrorResponse("duplicate productId in items.");
            }
        }

        return null;
    }

    private static string? NormalizeCustomerName(string? customerName) =>
        string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim();

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

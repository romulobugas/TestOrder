using System.Data;
using System.Text.Json;
using Dapper;
using MySqlConnector;
using TestOrder.Api.Models.Requests;
using TestOrder.Api.Models.Responses;

namespace TestOrder.Api.Controllers;

internal static class CreateOrderCommands
{
    private const string OrderCreatedEventType = "OrderCreated";
    private const string PendingStatus = "pending";
    private const string CreatedOrderStatus = "created";
    private const string ReservedStatus = "reserved";

    // READ COMMITTED + product_id ASC + SKIP LOCKED: reserva concorrente sem overbooking.
    private const string SelectProductsByIds = """
        SELECT id AS Id, name AS Name, unit_price AS UnitPrice
        FROM products
        WHERE id IN @ProductIds
        """;

    private const string SelectAvailableUnitsForUpdate = """
        SELECT id AS Id
        FROM inventory_units
        WHERE product_id = @ProductId AND status = 'available'
        ORDER BY id
        LIMIT @Quantity
        FOR UPDATE SKIP LOCKED
        """;

    private const string InsertOrder = """
        INSERT INTO orders (created_at, status, customer_name)
        VALUES (@CreatedAt, @Status, @CustomerName)
        """;

    private const string InsertOrderItem = """
        INSERT INTO order_items (order_id, product_id, quantity, unit_price)
        VALUES (@OrderId, @ProductId, @Quantity, @UnitPrice)
        """;

    private const string UpdateInventoryUnitsReserved = """
        UPDATE inventory_units
        SET status = @ReservedStatus
        WHERE id IN @UnitIds
        """;

    private const string InsertReservationUnit = """
        INSERT INTO order_reservation_units (order_id, inventory_unit_id)
        VALUES (@OrderId, @InventoryUnitId)
        """;

    private const string InsertProcessingEvent = """
        INSERT INTO order_processing_events (order_id, event_type, status, payload, created_at)
        VALUES (@OrderId, @EventType, @Status, @Payload, @CreatedAt)
        """;

    public static async Task<CreateOrderResult> ExecuteAsync(
        MySqlConnection connection,
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var sortedItems = request.Items!
            .OrderBy(item => item.ProductId)
            .ToList();

        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var productIds = sortedItems.Select(item => item.ProductId).ToList();
        var products = (await connection.QueryAsync<ProductRow>(
            SelectProductsByIds,
            new { ProductIds = productIds },
            transaction)).ToDictionary(product => product.Id);

        foreach (var item in sortedItems)
        {
            if (!products.ContainsKey(item.ProductId))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new CreateOrderBadRequest($"product not found: {item.ProductId}.");
            }
        }

        var reservedUnitIds = new List<long>();

        foreach (var item in sortedItems)
        {
            var unitIds = (await connection.QueryAsync<long>(
                SelectAvailableUnitsForUpdate,
                new { item.ProductId, Quantity = item.Quantity },
                transaction)).ToList();

            if (unitIds.Count < item.Quantity)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new CreateOrderConflict($"insufficient inventory for product {item.ProductId}.");
            }

            reservedUnitIds.AddRange(unitIds);
        }

        var createdAt = DateTime.UtcNow;

        await connection.ExecuteAsync(
            InsertOrder,
            new
            {
                CreatedAt = createdAt,
                Status = CreatedOrderStatus,
                request.CustomerName
            },
            transaction);

        var orderId = await connection.ExecuteScalarAsync<long>(
            "SELECT LAST_INSERT_ID()",
            transaction: transaction);

        var responseItems = new List<OrderItemResponse>(sortedItems.Count);
        decimal total = 0;

        foreach (var item in sortedItems)
        {
            var product = products[item.ProductId];

            await connection.ExecuteAsync(
                InsertOrderItem,
                new
                {
                    OrderId = orderId,
                    item.ProductId,
                    item.Quantity,
                    UnitPrice = product.UnitPrice
                },
                transaction);

            total += item.Quantity * product.UnitPrice;
            responseItems.Add(new OrderItemResponse(
                item.ProductId,
                product.Name,
                item.Quantity,
                product.UnitPrice));
        }

        await connection.ExecuteAsync(
            UpdateInventoryUnitsReserved,
            new { ReservedStatus, UnitIds = reservedUnitIds },
            transaction);

        foreach (var inventoryUnitId in reservedUnitIds)
        {
            await connection.ExecuteAsync(
                InsertReservationUnit,
                new { OrderId = orderId, InventoryUnitId = inventoryUnitId },
                transaction);
        }

        var payload = JsonSerializer.Serialize(new { orderId });

        await connection.ExecuteAsync(
            InsertProcessingEvent,
            new
            {
                OrderId = orderId,
                EventType = OrderCreatedEventType,
                Status = PendingStatus,
                Payload = payload,
                CreatedAt = createdAt
            },
            transaction);

        await transaction.CommitAsync(cancellationToken);

        return new CreateOrderSuccess(
            orderId,
            createdAt,
            CreatedOrderStatus,
            total,
            responseItems);
    }

    private sealed record ProductRow(int Id, string Name, decimal UnitPrice);
}

internal abstract record CreateOrderResult;

internal sealed record CreateOrderSuccess(
    long OrderId,
    DateTime CreatedAt,
    string Status,
    decimal Total,
    IReadOnlyList<OrderItemResponse> Items) : CreateOrderResult;

internal sealed record CreateOrderBadRequest(string Message) : CreateOrderResult;

internal sealed record CreateOrderConflict(string Message) : CreateOrderResult;

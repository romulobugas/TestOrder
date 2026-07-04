namespace TestOrder.Api.Controllers;

internal static class OrdersQueries
{
    // whereSql vem de OrdersController.BuildWhereClause — já parametrizado (@Status/@StartUtc/@EndExclusiveUtc),
    // nunca concatena valor de usuário diretamente na string.
    public static string BuildCountOrders(string whereSql) => $"SELECT COUNT(*) FROM orders o {whereSql}";

    public static string BuildPageOrders(string whereSql) => $"""
        SELECT o.id AS Id,
               o.created_at AS CreatedAt,
               o.customer_name AS CustomerName,
               o.status AS Status,
               (SELECT COALESCE(SUM(oi.quantity * oi.unit_price), 0)
                FROM order_items oi
                WHERE oi.order_id = o.id) AS Total
        FROM orders o
        {whereSql}
        ORDER BY o.created_at DESC, o.id DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    public const string OrderItemsByOrderIds = """
        SELECT oi.order_id AS OrderId,
               oi.product_id AS ProductId,
               p.name AS ProductName,
               oi.quantity AS Quantity,
               oi.unit_price AS UnitPrice
        FROM order_items oi
        INNER JOIN products p ON p.id = oi.product_id
        WHERE oi.order_id IN @OrderIds
        ORDER BY oi.order_id, oi.id
        """;

    public const string OrderById = """
        SELECT o.id AS Id,
               o.created_at AS CreatedAt,
               o.customer_name AS CustomerName,
               o.status AS Status,
               (SELECT COALESCE(SUM(oi.quantity * oi.unit_price), 0)
                FROM order_items oi
                WHERE oi.order_id = o.id) AS Total
        FROM orders o
        WHERE o.id = @Id
        """;

    public const string OrderItemsByOrderId = """
        SELECT oi.product_id AS ProductId,
               p.name AS ProductName,
               oi.quantity AS Quantity,
               oi.unit_price AS UnitPrice
        FROM order_items oi
        INNER JOIN products p ON p.id = oi.product_id
        WHERE oi.order_id = @OrderId
        ORDER BY oi.id
        """;
}

internal sealed record OrderPageRow(long Id, DateTime CreatedAt, string? CustomerName, string Status, decimal Total);

internal sealed record OrderItemRow(long OrderId, int ProductId, string ProductName, int Quantity, decimal UnitPrice);

internal sealed record OrderDetailRow(long Id, DateTime CreatedAt, string? CustomerName, string Status, decimal Total);

internal sealed record OrderItemDetailRow(int ProductId, string ProductName, int Quantity, decimal UnitPrice);

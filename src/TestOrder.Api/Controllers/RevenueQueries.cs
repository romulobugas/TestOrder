namespace TestOrder.Api.Controllers;

internal static class RevenueQueries
{
    // Intervalo semiaberto (>= start AND < end+1dia) evita problemas de fração de segundo
    // no limite superior e permite usar o índice de created_at sem função na coluna.
    // Dias sem pedido não aparecem aqui — são preenchidos com zero na camada C# (RevenueController).
    public const string DailyRevenueByRange = """
        SELECT DATE(o.created_at) AS Date,
               SUM(oi.quantity * oi.unit_price) AS Revenue,
               COUNT(DISTINCT o.id) AS OrderCount
        FROM orders o
        INNER JOIN order_items oi ON oi.order_id = o.id
        WHERE o.status = 'created'
          AND o.created_at >= @StartUtc
          AND o.created_at < @EndExclusiveUtc
        GROUP BY DATE(o.created_at)
        """;
}

internal sealed record RevenueDayRow(DateTime Date, decimal Revenue, long OrderCount);

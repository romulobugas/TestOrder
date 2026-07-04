namespace TestOrder.Api.Controllers;

internal static class RevenueQueries
{
    // Intervalo semiaberto (>= start AND < end+1dia) evita problemas de fração de segundo
    // no limite superior e permite usar o índice de created_at sem função na coluna.
    // Dias sem pedido não aparecem aqui — são preenchidos com zero na camada C# (RevenueController)
    // apenas quando as duas datas são conhecidas; caso contrário a lista já vem só com dias reais.
    public static string BuildDailyRevenueQuery(bool hasStart, bool hasEnd)
    {
        var conditions = new List<string> { "o.status = 'created'" };

        if (hasStart)
        {
            conditions.Add("o.created_at >= @StartUtc");
        }

        if (hasEnd)
        {
            conditions.Add("o.created_at < @EndExclusiveUtc");
        }

        return $"""
            SELECT DATE(o.created_at) AS Date,
                   SUM(oi.quantity * oi.unit_price) AS Revenue,
                   COUNT(DISTINCT o.id) AS OrderCount
            FROM orders o
            INNER JOIN order_items oi ON oi.order_id = o.id
            WHERE {string.Join(" AND ", conditions)}
            GROUP BY DATE(o.created_at)
            ORDER BY DATE(o.created_at)
            """;
    }
}

internal sealed record RevenueDayRow(DateTime Date, decimal Revenue, long OrderCount);

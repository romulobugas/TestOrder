using System.Globalization;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using TestOrder.Api.Models.Responses;

namespace TestOrder.Api.Controllers;

[ApiController]
[Route("api/revenue")]
public class RevenueController(MySqlConnection connection) : ControllerBase
{
    private const string DateFormat = "yyyy-MM-dd";
    private const int MaxRangeDays = 366;

    [HttpGet("daily")]
    public async Task<ActionResult<DailyRevenueResponse>> GetDailyRevenue(
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        CancellationToken cancellationToken)
    {
        DateOnly? start = null;
        DateOnly? end = null;

        if (!string.IsNullOrWhiteSpace(startDate))
        {
            if (!DateOnly.TryParseExact(startDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStart))
            {
                return BadRequest(new ErrorResponse("startDate must be a valid date in yyyy-MM-dd format."));
            }

            start = parsedStart;
        }

        if (!string.IsNullOrWhiteSpace(endDate))
        {
            if (!DateOnly.TryParseExact(endDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedEnd))
            {
                return BadRequest(new ErrorResponse("endDate must be a valid date in yyyy-MM-dd format."));
            }

            end = parsedEnd;
        }

        // O limite de 366 dias só faz sentido quando as duas datas são conhecidas (preenchimento de dias zerados
        // dia a dia). Com um lado aberto ou os dois vazios, não há laço de dias — não há como "explodir" a resposta.
        if (start is not null && end is not null)
        {
            if (start > end)
            {
                return BadRequest(new ErrorResponse("startDate must not be after endDate."));
            }

            if (end.Value.DayNumber - start.Value.DayNumber > MaxRangeDays - 1)
            {
                return BadRequest(new ErrorResponse($"Date range must not exceed {MaxRangeDays} days."));
            }
        }

        var startUtc = start?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusiveUtc = end?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        await connection.OpenAsync(cancellationToken);

        var rows = (await connection.QueryAsync<RevenueDayRow>(
            RevenueQueries.BuildDailyRevenueQuery(startUtc is not null, endExclusiveUtc is not null),
            new { StartUtc = startUtc, EndExclusiveUtc = endExclusiveUtc })).ToList();

        List<RevenueDayResponse> days;
        if (start is not null && end is not null)
        {
            // Intervalo fechado: preenche todo dia do intervalo, mesmo sem pedidos (comportamento original).
            var revenueByDate = rows.ToDictionary(row => DateOnly.FromDateTime(row.Date));
            days = [];
            for (var current = start.Value; current <= end.Value; current = current.AddDays(1))
            {
                var dateText = current.ToString(DateFormat, CultureInfo.InvariantCulture);
                days.Add(revenueByDate.TryGetValue(current, out var row)
                    ? new RevenueDayResponse(dateText, row.Revenue, (int)row.OrderCount)
                    : new RevenueDayResponse(dateText, 0m, 0));
            }
        }
        else
        {
            // Intervalo aberto (um ou os dois lados vazios): só os dias que realmente têm pedido — sem
            // limite conhecido para "explodir" em zeros, a lista vem só com o que a query já agregou.
            days = rows
                .Select(row => new RevenueDayResponse(
                    DateOnly.FromDateTime(row.Date).ToString(DateFormat, CultureInfo.InvariantCulture),
                    row.Revenue,
                    (int)row.OrderCount))
                .ToList();
        }

        var totalRevenue = days.Sum(day => day.Revenue);
        var totalOrders = days.Sum(day => day.OrderCount);

        return Ok(new DailyRevenueResponse(
            start?.ToString(DateFormat, CultureInfo.InvariantCulture),
            end?.ToString(DateFormat, CultureInfo.InvariantCulture),
            totalRevenue,
            totalOrders,
            days));
    }
}

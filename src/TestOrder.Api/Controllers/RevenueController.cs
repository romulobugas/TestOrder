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
        if (string.IsNullOrWhiteSpace(startDate))
        {
            return BadRequest(new ErrorResponse("startDate is required."));
        }

        if (string.IsNullOrWhiteSpace(endDate))
        {
            return BadRequest(new ErrorResponse("endDate is required."));
        }

        if (!DateOnly.TryParseExact(startDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
        {
            return BadRequest(new ErrorResponse("startDate must be a valid date in yyyy-MM-dd format."));
        }

        if (!DateOnly.TryParseExact(endDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
        {
            return BadRequest(new ErrorResponse("endDate must be a valid date in yyyy-MM-dd format."));
        }

        if (start > end)
        {
            return BadRequest(new ErrorResponse("startDate must not be after endDate."));
        }

        if (end.DayNumber - start.DayNumber > MaxRangeDays - 1)
        {
            return BadRequest(new ErrorResponse($"Date range must not exceed {MaxRangeDays} days."));
        }

        var startUtc = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusiveUtc = end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        await connection.OpenAsync(cancellationToken);

        var rows = (await connection.QueryAsync<RevenueDayRow>(
            RevenueQueries.DailyRevenueByRange,
            new { StartUtc = startUtc, EndExclusiveUtc = endExclusiveUtc })).ToList();

        var revenueByDate = rows.ToDictionary(row => DateOnly.FromDateTime(row.Date));

        var days = new List<RevenueDayResponse>();
        for (var current = start; current <= end; current = current.AddDays(1))
        {
            var dateText = current.ToString(DateFormat, CultureInfo.InvariantCulture);
            if (revenueByDate.TryGetValue(current, out var row))
            {
                days.Add(new RevenueDayResponse(dateText, row.Revenue, (int)row.OrderCount));
            }
            else
            {
                days.Add(new RevenueDayResponse(dateText, 0m, 0));
            }
        }

        var totalRevenue = days.Sum(day => day.Revenue);
        var totalOrders = days.Sum(day => day.OrderCount);

        return Ok(new DailyRevenueResponse(
            start.ToString(DateFormat, CultureInfo.InvariantCulture),
            end.ToString(DateFormat, CultureInfo.InvariantCulture),
            totalRevenue,
            totalOrders,
            days));
    }
}

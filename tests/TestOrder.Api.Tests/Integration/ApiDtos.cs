namespace TestOrder.Api.Tests.Integration;

public sealed record ProductDto(int Id, string Name, decimal UnitPrice);

public sealed record OrderItemDto(int ProductId, string ProductName, int Quantity, decimal UnitPrice);

public sealed record OrderDto(
    long Id,
    DateTime CreatedAt,
    string Status,
    decimal Total,
    IReadOnlyList<OrderItemDto> Items);

public sealed record PagedOrdersDto(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<OrderDto> Items);

public sealed record ErrorDto(string Error);

public sealed record CreateOrderRequestDto(
    string? CustomerName,
    IReadOnlyList<CreateOrderItemRequestDto>? Items);

public sealed record CreateOrderItemRequestDto(int ProductId, int Quantity);

public sealed record RevenueDayDto(string Date, decimal Revenue, int OrderCount);

public sealed record DailyRevenueDto(
    string StartDate,
    string EndDate,
    decimal TotalRevenue,
    int TotalOrders,
    IReadOnlyList<RevenueDayDto> Days);

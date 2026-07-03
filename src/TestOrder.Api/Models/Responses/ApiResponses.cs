namespace TestOrder.Api.Models.Responses;

public record ProductResponse(int Id, string Name, decimal UnitPrice);

public record OrderItemResponse(int ProductId, string ProductName, int Quantity, decimal UnitPrice);

public record OrderResponse(
    long Id,
    DateTime CreatedAt,
    string Status,
    decimal Total,
    IReadOnlyList<OrderItemResponse> Items);

public record PagedOrdersResponse(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<OrderResponse> Items);

public record ErrorResponse(string Error);

namespace TestOrder.Api.Models.Requests;

public record CreateOrderRequest(string? CustomerName, IReadOnlyList<CreateOrderItemRequest>? Items);

public record CreateOrderItemRequest(int ProductId, int Quantity);

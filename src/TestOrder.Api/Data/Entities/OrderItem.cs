namespace TestOrder.Api.Data.Entities;

public class OrderItem
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}

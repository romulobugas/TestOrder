namespace TestOrder.Api.Data.Entities;

public class Order
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public string Status { get; set; } = string.Empty;
}

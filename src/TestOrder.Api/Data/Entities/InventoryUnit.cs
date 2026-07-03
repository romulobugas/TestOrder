namespace TestOrder.Api.Data.Entities;

public class InventoryUnit
{
    public long Id { get; set; }

    public int ProductId { get; set; }

    public string Status { get; set; } = string.Empty;
}

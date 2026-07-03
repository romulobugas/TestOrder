namespace TestOrder.Api.Data.Entities;

public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int StockQuantity { get; set; }
}

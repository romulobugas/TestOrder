namespace TestOrder.Api.Data.Entities;

public class OrderReservationUnit
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public long InventoryUnitId { get; set; }
}

namespace TestOrder.Api.Data.Entities;

public class OrderProcessingEvent
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

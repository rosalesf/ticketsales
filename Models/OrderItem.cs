namespace TicketSales.Models;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public int TicketTypeId { get; set; }
    public string TicketTypeName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

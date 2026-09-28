namespace TicketSales.Models;

public class CartItem
{
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public int TicketTypeId { get; set; }
    public string TicketTypeName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal => UnitPrice * Quantity;
}

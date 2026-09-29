namespace TicketSales.Models;

public sealed record TicketQrCode(OrderItem Item, int Number, string DataUri);

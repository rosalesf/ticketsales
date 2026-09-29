using TicketSales.Models;

namespace TicketSales.Services;

public interface IOrderQrCodeGenerator
{
    byte[] GeneratePng(Order order, OrderItem item, int ticketNumber);
    string GenerateDataUri(Order order, OrderItem item, int ticketNumber);
}

using TicketSales.Models;

namespace TicketSales.Services;

public interface IOrderQrCodeGenerator
{
    byte[] GeneratePng(Order order);
    string GenerateDataUri(Order order);
}

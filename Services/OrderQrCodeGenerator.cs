using QRCoder;
using TicketSales.Models;

namespace TicketSales.Services;

public class OrderQrCodeGenerator(IPersonalDataProtector personalDataProtector) : IOrderQrCodeGenerator
{
    public string GenerateDataUri(Order order, OrderItem item, int ticketNumber)
    {
        return $"data:image/png;base64,{Convert.ToBase64String(GeneratePng(order, item, ticketNumber))}";
    }

    public byte[] GeneratePng(Order order, OrderItem item, int ticketNumber)
    {
        if (item.OrderId != order.Id || item.Id <= 0 || ticketNumber <= 0 || ticketNumber > item.Quantity)
            throw new ArgumentException("La entrada no pertenece a la orden o su número no es válido.");

        // Each unit is independently identifiable, even when several are from the same locality.
        var content = personalDataProtector.Protect($"{order.OrderNumber}:{item.Id}:{ticketNumber}");
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(12);
    }
}

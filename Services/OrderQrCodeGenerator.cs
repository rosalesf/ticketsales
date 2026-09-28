using QRCoder;
using TicketSales.Models;

namespace TicketSales.Services;

public class OrderQrCodeGenerator(IPersonalDataProtector personalDataProtector) : IOrderQrCodeGenerator
{
    public string GenerateDataUri(Order order)
    {
        return $"data:image/png;base64,{Convert.ToBase64String(GeneratePng(order))}";
    }

    public byte[] GeneratePng(Order order)
    {
        var content = personalDataProtector.Protect(order.OrderNumber);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(12);
    }
}

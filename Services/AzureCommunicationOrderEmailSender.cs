using System.Text;
using System.Text.Encodings.Web;
using Azure;
using Azure.Communication.Email;
using Microsoft.EntityFrameworkCore;
using TicketSales.Models;

namespace TicketSales.Services;

public sealed class AzureCommunicationOrderEmailSender(IConfiguration configuration, IOrderQrCodeGenerator orderQrCodeGenerator, TicketSales.Data.TicketDbContext db, ILogger<AzureCommunicationOrderEmailSender> logger) : IOrderEmailSender
{
    public async Task SendPurchaseConfirmationAsync(Order order, string recipientEmail, string customerName, CancellationToken cancellationToken = default)
    {
        var connectionString = configuration["AzureCommunicationServices:ConnectionString"];
        var senderAddress = configuration["AzureCommunicationServices:SenderAddress"];
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(senderAddress))
        {
            logger.LogWarning("La confirmación de la orden {OrderNumber} no se envió porque Azure Communication Services no está configurado.", order.OrderNumber);
            return;
        }

        const string qrContentId = "ventatickets-order-qr";
        var content = new EmailContent($"Confirmación de compra {order.OrderNumber}")
        {
            Html = await BuildHtmlAsync(order, customerName, qrContentId, cancellationToken),
            PlainText = BuildPlainText(order, customerName)
        };
        var recipients = new EmailRecipients([new EmailAddress(recipientEmail, customerName)]);
        var message = new EmailMessage(senderAddress, recipients, content);
        var qrAttachment = new EmailAttachment($"orden-{order.OrderNumber}.png", "image/png", BinaryData.FromBytes(orderQrCodeGenerator.GeneratePng(order)))
        {
            ContentId = qrContentId
        };
        message.Attachments.Add(qrAttachment);

        var client = new EmailClient(connectionString, new EmailClientOptions { Retry = { MaxRetries = 0 } });
        var operation = await client.SendAsync(WaitUntil.Started, message, cancellationToken);
        logger.LogInformation("Correo de confirmación de la orden {OrderNumber} encolado. Operación ACS: {OperationId}.", order.OrderNumber, operation.Id);
    }

    private static string BuildPlainText(Order order, string customerName)
    {
        var lines = order.Items.Select(item => $"- {item.EventName} · {item.TicketTypeName} × {item.Quantity}: Q {(item.UnitPrice * item.Quantity):N2}");
        return $"Hola {customerName},\n\nTu pago fue aprobado.\nOrden: {order.OrderNumber}\nReferencia: {order.PaymentReference}\n\nEntradas:\n{string.Join("\n", lines)}\n\nTotal pagado: Q {order.Total:N2}\n\nEscanea el código QR incluido en este correo para validar tu orden.\n\nVentaTickets";
    }

    private async Task<string> BuildHtmlAsync(Order order, string customerName, string qrContentId, CancellationToken cancellationToken)
    {
        string Encode(string value) => HtmlEncoder.Default.Encode(value);
        var items = new StringBuilder();
        foreach (var item in order.Items)
        {
            items.Append($"<tr><td style=\"padding:12px 0;border-bottom:1px solid #dfe3da\">{Encode(item.EventName)}<br><span style=\"color:#69716d;font-size:13px\">{Encode(item.TicketTypeName)} · {item.Quantity} × Q {item.UnitPrice:N2}</span></td><td style=\"padding:12px 0;border-bottom:1px solid #dfe3da;text-align:right;white-space:nowrap\">Q {(item.UnitPrice * item.Quantity):N2}</td></tr>");
        }

        var qrBlock = $"<table role=\"presentation\" style=\"width:100%;margin:28px 0 0;border-collapse:separate;border-spacing:0;background:#f4f5f0;border-radius:10px\"><tr><td style=\"width:200px;padding:18px;vertical-align:middle;text-align:center\"><img src=\"cid:{Encode(qrContentId)}\" alt=\"Código QR de la orden {Encode(order.OrderNumber)}\" width=\"160\" height=\"160\" style=\"display:block;width:160px;height:160px;margin:0 auto\"></td><td style=\"padding:18px 18px 18px 0;vertical-align:middle\"><strong style=\"display:block;font-size:16px;margin-bottom:7px\">Código de validación</strong><span style=\"display:block;color:#69716d;font-size:13px;line-height:1.5\">Presenta este código QR al ingresar al evento.</span></td></tr></table>";
        var template = await db.PurchaseConfirmationEmailTemplates.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Html).FirstOrDefaultAsync(cancellationToken) ?? PurchaseConfirmationEmailTemplateDefaults.Html;
        return template.Replace("{{CustomerName}}", Encode(customerName), StringComparison.Ordinal)
            .Replace("{{OrderNumber}}", Encode(order.OrderNumber), StringComparison.Ordinal)
            .Replace("{{PaymentReference}}", Encode(order.PaymentReference ?? string.Empty), StringComparison.Ordinal)
            .Replace("{{OrderItems}}", items.ToString(), StringComparison.Ordinal)
            .Replace("{{Total}}", $"Q {order.Total:N2}", StringComparison.Ordinal)
            .Replace("{{QrCode}}", qrBlock, StringComparison.Ordinal);
    }
}

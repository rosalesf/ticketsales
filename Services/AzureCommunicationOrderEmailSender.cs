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

        var tickets = ExpandTickets(order).ToArray();
        var content = new EmailContent($"Confirmación de compra {order.OrderNumber}")
        {
            Html = await BuildHtmlAsync(order, customerName, tickets, cancellationToken),
            PlainText = BuildPlainText(order, customerName, tickets.Length)
        };
        var recipients = new EmailRecipients([new EmailAddress(recipientEmail, customerName)]);
        var message = new EmailMessage(senderAddress, recipients, content);
        foreach (var ticket in tickets)
        {
            var qrAttachment = new EmailAttachment($"entrada-{order.OrderNumber}-{ticket.Item.Id}-{ticket.Number}.png", "image/png",
                BinaryData.FromBytes(orderQrCodeGenerator.GeneratePng(order, ticket.Item, ticket.Number)))
            {
                ContentId = ticket.ContentId
            };
            message.Attachments.Add(qrAttachment);
        }

        var client = new EmailClient(connectionString, new EmailClientOptions { Retry = { MaxRetries = 0 } });
        var operation = await client.SendAsync(WaitUntil.Started, message, cancellationToken);
        logger.LogInformation("Correo de confirmación de la orden {OrderNumber} encolado. Operación ACS: {OperationId}.", order.OrderNumber, operation.Id);
    }

    private static string BuildPlainText(Order order, string customerName, int ticketCount)
    {
        var lines = order.Items.Select(item => $"- {item.EventName} · {item.TicketTypeName} × {item.Quantity}: Q {(item.UnitPrice * item.Quantity):N2}");
        return $"Hola {customerName},\n\nTu pago fue aprobado.\nOrden: {order.OrderNumber}\nReferencia: {order.PaymentReference}\n\nEntradas:\n{string.Join("\n", lines)}\n\nTotal pagado: Q {order.Total:N2}\n\nIncluimos {ticketCount} código(s) QR: presenta el correspondiente a cada entrada al ingresar al evento.\n\nVentaTickets";
    }

    private async Task<string> BuildHtmlAsync(Order order, string customerName, IReadOnlyCollection<TicketQr> tickets, CancellationToken cancellationToken)
    {
        string Encode(string value) => HtmlEncoder.Default.Encode(value);
        var items = new StringBuilder();
        foreach (var item in order.Items)
        {
            items.Append($"<tr><td style=\"padding:12px 0;border-bottom:1px solid #dfe3da\">{Encode(item.EventName)}<br><span style=\"color:#69716d;font-size:13px\">{Encode(item.TicketTypeName)} · {item.Quantity} × Q {item.UnitPrice:N2}</span></td><td style=\"padding:12px 0;border-bottom:1px solid #dfe3da;text-align:right;white-space:nowrap\">Q {(item.UnitPrice * item.Quantity):N2}</td></tr>");
        }

        var qrBlock = string.Concat(tickets.Select(ticket => $"<table role=\"presentation\" style=\"width:100%;margin:18px 0 0;border-collapse:separate;border-spacing:0;background:#f4f5f0;border-radius:10px\"><tr><td style=\"width:200px;padding:18px;vertical-align:middle;text-align:center\"><img src=\"cid:{Encode(ticket.ContentId)}\" alt=\"Código QR para {Encode(ticket.Item.TicketTypeName)} entrada {ticket.Number}\" width=\"160\" height=\"160\" style=\"display:block;width:160px;height:160px;margin:0 auto\"></td><td style=\"padding:18px 18px 18px 0;vertical-align:middle\"><strong style=\"display:block;font-size:16px;margin-bottom:7px\">{Encode(ticket.Item.EventName)} · {Encode(ticket.Item.TicketTypeName)}</strong><span style=\"display:block;color:#69716d;font-size:13px;line-height:1.5\">Entrada {ticket.Number} de {ticket.Item.Quantity}. Presenta este código QR al ingresar al evento.</span></td></tr></table>"));
        var template = await db.PurchaseConfirmationEmailTemplates.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Html).FirstOrDefaultAsync(cancellationToken) ?? PurchaseConfirmationEmailTemplateDefaults.Html;
        return template.Replace("{{CustomerName}}", Encode(customerName), StringComparison.Ordinal)
            .Replace("{{OrderNumber}}", Encode(order.OrderNumber), StringComparison.Ordinal)
            .Replace("{{PaymentReference}}", Encode(order.PaymentReference ?? string.Empty), StringComparison.Ordinal)
            .Replace("{{OrderItems}}", items.ToString(), StringComparison.Ordinal)
            .Replace("{{Total}}", $"Q {order.Total:N2}", StringComparison.Ordinal)
            .Replace("{{QrCode}}", qrBlock, StringComparison.Ordinal);
    }

    private static IEnumerable<TicketQr> ExpandTickets(Order order) => order.Items
        .OrderBy(item => item.Id)
        .SelectMany(item => Enumerable.Range(1, item.Quantity)
            .Select(number => new TicketQr(item, number, $"ventatickets-ticket-{item.Id}-{number}")));

    private sealed record TicketQr(OrderItem Item, int Number, string ContentId);
}

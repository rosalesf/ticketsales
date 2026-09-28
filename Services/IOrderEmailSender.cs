using TicketSales.Models;

namespace TicketSales.Services;

public interface IOrderEmailSender
{
    Task SendPurchaseConfirmationAsync(Order order, string recipientEmail, string customerName, CancellationToken cancellationToken = default);
}

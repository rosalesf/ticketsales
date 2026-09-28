namespace TicketSales.Services;

public interface IAccountEmailSender
{
    Task SendPasswordResetAsync(string recipientEmail, string resetUrl, int expirationMinutes, CancellationToken cancellationToken = default);
}

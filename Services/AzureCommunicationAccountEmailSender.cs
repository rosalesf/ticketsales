using System.Text.Encodings.Web;
using Azure;
using Azure.Core.Pipeline;
using Azure.Communication.Email;

namespace TicketSales.Services;

public sealed class AzureCommunicationAccountEmailSender(IConfiguration configuration, ILogger<AzureCommunicationAccountEmailSender> logger) : IAccountEmailSender
{
    private static readonly HttpClient DirectHttpClient = new(new SocketsHttpHandler { UseProxy = false });

    public async Task SendPasswordResetAsync(string recipientEmail, string resetUrl, int expirationMinutes, CancellationToken cancellationToken = default)
    {
        var connectionString = configuration["AzureCommunicationServices:ConnectionString"];
        var senderAddress = configuration["AzureCommunicationServices:SenderAddress"];
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(senderAddress))
            throw new InvalidOperationException("El servicio de correo no está configurado.");

        var url = HtmlEncoder.Default.Encode(resetUrl);
        var content = new EmailContent("Restablece tu contraseña · VentaTickets")
        {
            PlainText = $"Recibimos una solicitud para restablecer tu contraseña. Abre este enlace para definir una nueva contraseña: {resetUrl}\n\nEl enlace y su código vencen en {expirationMinutes} minutos. Si no solicitaste este cambio, ignora este correo.",
            Html = $"<div style=\"font-family:Arial,sans-serif;color:#17201c;max-width:560px;margin:auto\"><h2>Restablece tu contraseña</h2><p>Recibimos una solicitud para restablecer la contraseña de tu cuenta en VentaTickets.</p><p><a href=\"{url}\" style=\"display:inline-block;background:#17201c;color:#fff;padding:12px 18px;border-radius:6px;text-decoration:none;font-weight:bold\">Restablecer contraseña</a></p><p style=\"color:#69716d\">El enlace y su código de seguridad vencen en {expirationMinutes} minutos.</p><p style=\"color:#69716d\">Si no solicitaste este cambio, puedes ignorar este correo.</p></div>"
        };
        var message = new EmailMessage(senderAddress, new EmailRecipients([new EmailAddress(recipientEmail)]), content);
        var client = new EmailClient(connectionString, new EmailClientOptions
        {
            Transport = new HttpClientTransport(DirectHttpClient),
            Retry = { MaxRetries = 0 }
        });
        var operation = await client.SendAsync(WaitUntil.Started, message, cancellationToken);
        logger.LogInformation("Correo de restablecimiento encolado. Operación ACS: {OperationId}.", operation.Id);
    }
}

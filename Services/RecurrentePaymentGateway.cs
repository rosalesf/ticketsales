using System.Text;
using System.Text.Json;

namespace TicketSales.Services;

public sealed class RecurrentePaymentGateway(HttpClient client, IConfiguration configuration) : IPaymentGateway
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(configuration["Recurrente:SecretKey"])
        && !string.IsNullOrWhiteSpace(configuration["Recurrente:WebhookSecret"])
        && Uri.TryCreate(configuration["Recurrente:PublicBaseUrl"], UriKind.Absolute, out var uri) && uri.Scheme == "https";

    public Task<JsonElement> CreateCheckoutAsync(string payload, string idempotencyKey, CancellationToken cancellationToken)
        => SendAsync(HttpMethod.Post, "checkouts", payload, idempotencyKey, cancellationToken);

    public Task<JsonElement> UpdateCheckoutAsync(string id, string payload, string idempotencyKey, CancellationToken cancellationToken)
        => SendAsync(HttpMethod.Patch, "checkouts/" + Uri.EscapeDataString(id), payload, idempotencyKey, cancellationToken);

    public Task<JsonElement> GetCheckoutAsync(string id, CancellationToken cancellationToken)
        => SendAsync(HttpMethod.Get, "checkouts/" + Uri.EscapeDataString(id), null, null, cancellationToken);

    public Task<JsonElement> CreateCustomerAsync(string payload, string idempotencyKey, CancellationToken cancellationToken)
        => SendAsync(HttpMethod.Post, "customers", payload, idempotencyKey, cancellationToken);

    public async Task UpdateCustomerAsync(string id, string payload, CancellationToken cancellationToken)
        => _ = await SendAsync(HttpMethod.Put, "customers/" + Uri.EscapeDataString(id), payload, null, cancellationToken);

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, string? payload, string? key, CancellationToken ct)
    {
        if (!IsConfigured) throw new InvalidOperationException("Recurrente no está configurado.");
        using var request = new HttpRequestMessage(method, "https://app.recurrente.com/api/" + path);
        request.Headers.Add("X-SECRET-KEY", configuration["Recurrente:SecretKey"]);
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        if (payload is not null) request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            var reason = "El proveedor no pudo procesar la solicitud.";
            try
            {
                using var errorDocument = JsonDocument.Parse(responseBody);
                var root = errorDocument.RootElement;
                var code = root.TryGetProperty("code", out var codeValue) ? codeValue.GetString() : null;
                var message = root.TryGetProperty("message", out var messageValue) ? messageValue.GetString() : null;
                reason = !string.IsNullOrWhiteSpace(message) ? message! : !string.IsNullOrWhiteSpace(code) ? code! : reason;
            }
            catch (JsonException) { }
            throw new InvalidOperationException("Recurrente rechazó el checkout: " + reason);
        }
        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.Clone();
    }
}

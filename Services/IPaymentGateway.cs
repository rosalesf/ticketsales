using System.Text.Json;

namespace TicketSales.Services;

public interface IPaymentGateway
{
    bool IsConfigured { get; }
    Task<JsonElement> CreateCheckoutAsync(string payload, string idempotencyKey, CancellationToken cancellationToken);
    Task<JsonElement> UpdateCheckoutAsync(string id, string payload, string idempotencyKey, CancellationToken cancellationToken);
    Task<JsonElement> GetCheckoutAsync(string id, CancellationToken cancellationToken);
    Task<JsonElement> CreateCustomerAsync(string payload, string idempotencyKey, CancellationToken cancellationToken);
    Task UpdateCustomerAsync(string id, string payload, CancellationToken cancellationToken);
}

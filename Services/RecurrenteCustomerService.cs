using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TicketSales.Data;
using TicketSales.Models;

namespace TicketSales.Services;

public sealed class RecurrenteCustomerService(
    IPaymentGateway gateway,
    AdminIdentityDbContext identityDb,
    IPersonalDataProtector protector) : IRecurrenteCustomerService
{
    public async Task SynchronizeAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.RecurrenteCustomerId is not null) return;

        var email = protector.Unprotect(order.CustomerEmail).Trim();
        var name = protector.Unprotect(order.CustomerName).Trim();
        var phone = protector.Unprotect(order.CustomerPhone).Trim();
        var nit = order.InvoiceType == "NIT" && order.InvoiceNit is not null
            ? protector.Unprotect(order.InvoiceNit).Trim() : null;

        // Recurrente creates or finds a customer by email. This key makes retries of this order safe.
        var created = await gateway.CreateCustomerAsync(JsonSerializer.Serialize(new { email, full_name = name, phone, nit }),
            "customer-" + order.OrderNumber, cancellationToken);
        var customerId = created.TryGetProperty("id", out var id) ? id.GetString() : null;
        if (string.IsNullOrWhiteSpace(customerId) || customerId.Length > 128)
            throw new InvalidOperationException("Recurrente no devolvió un cliente válido.");

        // A found customer may have old details; make the order's already-validated details authoritative.
        await gateway.UpdateCustomerAsync(customerId, JsonSerializer.Serialize(new { name, phone, nit }), cancellationToken);
        order.RecurrenteCustomerId = customerId;

        if (order.CustomerUserId is null) return;
        var profile = await identityDb.CustomerProfiles.SingleOrDefaultAsync(p => p.UserId == order.CustomerUserId, cancellationToken);
        if (profile is not null && profile.RecurrenteCustomerId != customerId)
        {
            profile.RecurrenteCustomerId = customerId;
            await identityDb.SaveChangesAsync(cancellationToken);
        }
    }
}

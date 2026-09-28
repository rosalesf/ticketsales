using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TicketSales.Data;
using TicketSales.Models;

namespace TicketSales.Services;

public sealed class PaymentOrderService(TicketDbContext db, IPaymentGateway gateway, IConfiguration configuration, IRecurrenteCustomerService customers)
{
    public bool IsConfigured => gateway.IsConfigured;

    // SQL application locks coordinate requests, webhooks and workers across app instances.
    public static async Task<IDbContextTransaction> LockAsync(TicketDbContext db, string key, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource={key}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=25000; IF @r < 0 THROW 51000, 'Payment lock unavailable', 1;", ct);
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    public async Task<Order> ReserveAsync(Order order, CancellationToken ct)
    {
        if (!IsConfigured) throw new InvalidOperationException("Los pagos con tarjeta todavía no están disponibles.");
        await using var transaction = await LockAsync(db, "checkout:" + order.PaymentAccessToken, ct);
        var existing = await db.Orders.FirstOrDefaultAsync(o => o.PaymentAccessToken == order.PaymentAccessToken, ct);
        if (existing is not null) return existing;
        if (order.Total < 5 || order.Total * 100 != decimal.Truncate(order.Total * 100) || order.Total * 100 > int.MaxValue)
            throw new InvalidOperationException("El total debe ser de al menos Q5.00 y tener como máximo dos decimales.");
        foreach (var item in order.Items.OrderBy(i => i.TicketTypeId))
        {
            if (item.Quantity <= 0) throw new InvalidOperationException("La cantidad de entradas no es válida.");
            var affected = await db.TicketTypes.Where(t => t.Id == item.TicketTypeId && t.EventId == item.EventId
                && t.Event.Status == EventStatuses.Published && t.Event.StartsAt > DateTime.Now
                && t.AvailableTickets >= item.Quantity && t.Price == item.UnitPrice)
                .ExecuteUpdateAsync(set => set.SetProperty(t => t.AvailableTickets, t => t.AvailableTickets - item.Quantity), ct);
            if (affected != 1) throw new InvalidOperationException("Cambió la disponibilidad o el precio. Revisa tu carrito.");
        }
        order.InventoryReserved = true;
        order.ReservationExpiresAt = DateTime.UtcNow.AddMinutes(GetReservationMinutes());
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return order;
    }

    public async Task EnsureCheckoutAsync(int orderId, CancellationToken ct)
    {
        string? payload = null;
        await using (var transaction = await LockAsync(db, "payment:" + orderId, ct))
        {
            db.ChangeTracker.Clear();
            var order = await db.Orders.Include(o => o.Items).SingleAsync(o => o.Id == orderId, ct);
            if (order.RecurrenteCheckoutId is not null || !order.InventoryReserved) return;
            if (order.ReservationExpiresAt <= DateTime.UtcNow)
            {
                // A session without a persisted URL has never been exposed to the buyer.
                await ReleaseAsync(order, ct);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return;
            }

            await customers.SynchronizeAsync(order, ct);
            order.CheckoutRequestJson ??= CreateCheckoutRequest(order);
            payload = order.CheckoutRequestJson;
            // Persist identical bytes before the remote checkout call so a retry is safe.
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        var result = await gateway.CreateCheckoutAsync(payload!, (await db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId).Select(o => o.OrderNumber).SingleAsync(ct)), ct);
        var id = result.GetProperty("id").GetString();
        var url = result.GetProperty("checkout_url").GetString();
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != "https" || uri.Host != "app.recurrente.com" || uri.Port != 443 || uri.UserInfo.Length != 0)
            throw new InvalidOperationException("Respuesta de checkout no válida.");

        await using var resultTransaction = await LockAsync(db, "payment:" + orderId, ct);
        db.ChangeTracker.Clear();
        var persisted = await db.Orders.Include(o => o.Items).SingleAsync(o => o.Id == orderId, ct);
        if (persisted.RecurrenteCheckoutId is null)
        {
            persisted.RecurrenteCheckoutId = id;
            persisted.RecurrenteCheckoutUrl = url;
            await db.SaveChangesAsync(ct);
        }
        await resultTransaction.CommitAsync(ct);
    }

    public async Task SynchronizeAsync(int orderId, CancellationToken ct, bool failed = false)
    {
        await using var transaction = await LockAsync(db, "payment:" + orderId, ct);
        db.ChangeTracker.Clear();
        var order = await db.Orders.Include(o => o.Items).SingleAsync(o => o.Id == orderId, ct);
        if (order.RecurrenteCheckoutId is null || order.PaymentStatus == "Approved" || order.OrderStatus == "PaymentReview") return;
        var checkout = await gateway.GetCheckoutAsync(order.RecurrenteCheckoutId, ct);
        if (checkout.GetProperty("id").GetString() != order.RecurrenteCheckoutId)
            throw new InvalidOperationException("El checkout no corresponde a la orden.");
        var status = checkout.GetProperty("status").GetString();
        if (status == "paid")
        {
            var valid = checkout.TryGetProperty("total_in_cents", out var amount) && amount.TryGetInt64(out var cents)
                && cents == order.Total * 100 && checkout.TryGetProperty("currency", out var currency) && currency.GetString() == "GTQ";
            if (!valid || !order.InventoryReserved)
            {
                order.PaymentStatus = "Review";
                order.OrderStatus = "PaymentReview";
            }
            else
            {
                order.PaymentStatus = "Approved";
                order.OrderStatus = "Confirmed";
                order.InventoryReserved = false; // Reservation becomes a sale; do not decrement again.
                order.PaymentReference = checkout.TryGetProperty("payment", out var payment) && payment.ValueKind == JsonValueKind.Object
                    && payment.TryGetProperty("id", out var reference) ? reference.GetString() : order.RecurrenteCheckoutId;
            }
        }
        else if (status == "expired" || (status == "unpaid" && order.ReservationExpiresAt <= DateTime.UtcNow))
            await ReleaseAsync(order, ct);
        else if (status == "payment_in_progress") order.PaymentStatus = "Pending";
        else if (status == "unpaid" && failed) order.PaymentStatus = "Rejected";
        // A local expiry is authoritative for inventory. A late payment moves to review because InventoryReserved is false.
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task UpdateReservationAsync(string token, IReadOnlyCollection<CartItem> cart, CancellationToken ct)
    {
        await using var transaction = await LockAsync(db, "checkout:" + token, ct);
        db.ChangeTracker.Clear();
        var order = await db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.PaymentAccessToken == token, ct);
        if (order is null || !order.InventoryReserved || order.PaymentStatus != "Pending") return;
        if (order.ReservationExpiresAt <= DateTime.UtcNow)
        {
            await ReleaseAsync(order, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            throw new InvalidOperationException("La reserva venció. Las entradas fueron liberadas; inicia un nuevo checkout.");
        }

        var desired = cart.ToDictionary(x => x.TicketTypeId);
        if (desired.Keys.Except(order.Items.Select(x => x.TicketTypeId)).Any())
            throw new InvalidOperationException("No puedes agregar otro tipo de entrada mientras este pago está activo. Vuelve a iniciar el checkout.");

        foreach (var item in order.Items)
        {
            var newQuantity = desired.GetValueOrDefault(item.TicketTypeId)?.Quantity ?? 0;
            var difference = newQuantity - item.Quantity;
            if (difference > 0)
            {
                var affected = await db.TicketTypes.Where(t => t.Id == item.TicketTypeId && t.EventId == item.EventId
                    && t.Event.Status == EventStatuses.Published && t.Event.StartsAt > DateTime.Now
                    && t.Price == item.UnitPrice && t.AvailableTickets >= difference)
                    .ExecuteUpdateAsync(set => set.SetProperty(t => t.AvailableTickets, t => t.AvailableTickets - difference), ct);
                if (affected != 1) throw new InvalidOperationException("No hay más entradas disponibles para aumentar esta reserva.");
            }
            else if (difference < 0)
                await db.TicketTypes.Where(t => t.Id == item.TicketTypeId)
                    .ExecuteUpdateAsync(set => set.SetProperty(t => t.AvailableTickets, t => t.AvailableTickets - difference), ct);
        }

        if (order.RecurrenteCheckoutId is not null)
        {
            var remote = await gateway.GetCheckoutAsync(order.RecurrenteCheckoutId, ct);
            if (remote.GetProperty("status").GetString() != "unpaid")
                throw new InvalidOperationException("El pago ya está en proceso y no puede modificarse.");

            var providerItems = remote.GetProperty("items").EnumerateArray()
                .Where(item => item.TryGetProperty("id", out _) && item.TryGetProperty("metadata", out var metadata)
                    && metadata.TryGetProperty("ticket_type_id", out var ticketType)
                    && int.TryParse(ticketType.GetString(), out _))
                .ToDictionary(item => int.Parse(item.GetProperty("metadata").GetProperty("ticket_type_id").GetString()!), item => item.GetProperty("id").GetString()!);
            var changes = order.Items.Select(item => new { ProviderItemId = providerItems.GetValueOrDefault(item.TicketTypeId), Quantity = desired.GetValueOrDefault(item.TicketTypeId)?.Quantity, item.TicketTypeId }).ToArray();
            if (changes.Any(x => string.IsNullOrWhiteSpace(x.ProviderItemId)))
                throw new InvalidOperationException("Esta reserva se creó antes de habilitar cambios de cantidad. Vuelve a iniciar el checkout.");
            var mutations = changes.Select(change => change.Quantity is null
                ? (object)new { id = change.ProviderItemId, deleted = true }
                : new { id = change.ProviderItemId, quantity = change.Quantity.Value }).ToArray();

            var response = await gateway.UpdateCheckoutAsync(order.RecurrenteCheckoutId,
                JsonSerializer.Serialize(new { items = mutations }),
                "cart-" + order.Id + "-" + string.Join('-', changes.Select(x => x.Quantity ?? 0)), ct);
            var expected = cart.Sum(item => item.UnitPrice * item.Quantity) * 100;
            if (!response.TryGetProperty("total_in_cents", out var cents) || cents.GetDecimal() != expected)
                throw new InvalidOperationException("No se pudo actualizar el total del pago.");
        }

        foreach (var item in order.Items.ToList())
        {
            var current = desired.GetValueOrDefault(item.TicketTypeId);
            if (current is null) db.OrderItems.Remove(item);
            else item.Quantity = current.Quantity;
        }
        order.Total = cart.Sum(item => item.UnitPrice * item.Quantity);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task ReleaseAsync(Order order, CancellationToken ct)
    {
        if (order.InventoryReserved)
            foreach (var item in order.Items.OrderBy(i => i.TicketTypeId))
                await db.TicketTypes.Where(t => t.Id == item.TicketTypeId)
                    .ExecuteUpdateAsync(set => set.SetProperty(t => t.AvailableTickets, t => t.AvailableTickets + item.Quantity), ct);
        order.InventoryReserved = false;
        order.PaymentStatus = "Expired";
        order.OrderStatus = "Expired";
    }

    private int GetReservationMinutes()
    {
        var configured = configuration.GetValue<int?>("Recurrente:ReservationMinutes") ?? 20;
        if (configured is < 1 or > 1440)
            throw new InvalidOperationException("Recurrente:ReservationMinutes debe estar entre 1 y 1440.");
        return configured;
    }

    private string CreateCheckoutRequest(Order order)
    {
        var baseUrl = configuration["Recurrente:PublicBaseUrl"]!.TrimEnd('/');
        var returnUrl = baseUrl + "/Home/PaymentReturn?token=" + order.PaymentAccessToken;
        var cancelUrl = baseUrl + "/Home/PaymentCancelled?token=" + order.PaymentAccessToken;
        return JsonSerializer.Serialize(new
        {
            items = order.Items.Select(item => new { name = item.EventName + " · " + item.TicketTypeName,
                amount_in_cents = checked((int)(item.UnitPrice * 100)), currency = "GTQ", quantity = item.Quantity,
                charge_type = "one_time", payment_method_types = new[] { "card" }, available_installments = Array.Empty<int>(),
                billing_info_requirement = "none", has_dynamic_pricing = false,
                metadata = new Dictionary<string, string> { ["ticket_type_id"] = item.TicketTypeId.ToString() } }).ToArray(),
            customer_id = order.RecurrenteCustomerId,
            success_url = returnUrl, cancel_url = cancelUrl,
            expires_at = order.ReservationExpiresAt!.Value.ToString("O"), metadata = order.OrderNumber
        });
    }
}

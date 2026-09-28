using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketSales.Data;
using TicketSales.Services;

namespace TicketSales.Controllers;

[ApiController]
public sealed class RecurrenteWebhookController(TicketDbContext db, PaymentOrderService payments, IConfiguration configuration) : ControllerBase
{
    [HttpPost("/webhooks/recurrente")]
    [RequestSizeLimit(262144)]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var body = new MemoryStream();
        await Request.Body.CopyToAsync(body, ct);
        var bytes = body.ToArray();
        if (!RecurrenteWebhookSignature.Verify(configuration["Recurrente:WebhookSecret"] ?? "",
            Request.Headers["svix-id"].ToString(), Request.Headers["svix-timestamp"].ToString(),
            Request.Headers["svix-signature"].ToString(), bytes, DateTimeOffset.UtcNow)) return Unauthorized();
        JsonDocument document;
        try { document = JsonDocument.Parse(bytes); }
        catch (JsonException) { return BadRequest(); }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return BadRequest();
            if (!root.TryGetProperty("event_type", out var type) || type.ValueKind != JsonValueKind.String
                || type.GetString() is not ("intent.succeeded" or "intent.failed" or "intent.pending" or "intent.canceled")) return Ok();
            if (!root.TryGetProperty("checkout", out var checkout) || checkout.ValueKind != JsonValueKind.Object
                || !checkout.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String) return BadRequest();
            var checkoutId = id.GetString();
            var orderId = await db.Orders.Where(o => o.RecurrenteCheckoutId == checkoutId).Select(o => (int?)o.Id).SingleOrDefaultAsync(ct);
            // A webhook can race the create response. Return 503 so Recurrente retries.
            if (orderId is null) return StatusCode(503);
            await payments.SynchronizeAsync(orderId.Value, ct, type.GetString() == "intent.failed");
            return Ok();
        }
    }
}

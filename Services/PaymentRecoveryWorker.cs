using Microsoft.EntityFrameworkCore;
using TicketSales.Data;

namespace TicketSales.Services;

public sealed class PaymentRecoveryWorker(IServiceScopeFactory scopes, ILogger<PaymentRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
                var payments = scope.ServiceProvider.GetRequiredService<PaymentOrderService>();
                if (!payments.IsConfigured) continue;
                var ids = await db.Orders.AsNoTracking().Where(o => o.PaymentStatus == "Approved" && o.ConfirmationEmailSentAt == null)
                    .OrderBy(o => o.Id).Select(o => o.Id).ToListAsync(stoppingToken);
                foreach (var id in ids)
                {
                    try
                    {
                     await SendEmailAsync(scope.ServiceProvider, id, stoppingToken);
                    }
                    catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                    { logger.LogWarning(exception, "No se pudo enviar la confirmación de la orden {OrderId}. Se reintentará.", id); }
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            { logger.LogWarning(exception, "Recuperación de pagos no disponible."); }
        }
    }

    private static async Task SendEmailAsync(IServiceProvider services, int id, CancellationToken ct)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        if (string.IsNullOrWhiteSpace(configuration["AzureCommunicationServices:ConnectionString"]) ||
            string.IsNullOrWhiteSpace(configuration["AzureCommunicationServices:SenderAddress"])) return;
        var db = services.GetRequiredService<TicketDbContext>();
        await using var transaction = await PaymentOrderService.LockAsync(db, "payment:" + id, ct);
        db.ChangeTracker.Clear();
        var order = await db.Orders.Include(o => o.Items).SingleAsync(o => o.Id == id, ct);
        if (order.PaymentStatus != "Approved" || order.ConfirmationEmailSentAt is not null) return;
        var protector = services.GetRequiredService<IPersonalDataProtector>();
        await services.GetRequiredService<IOrderEmailSender>().SendPurchaseConfirmationAsync(order,
            protector.Unprotect(order.CustomerEmail), protector.Unprotect(order.CustomerName), ct);
        order.ConfirmationEmailSentAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
